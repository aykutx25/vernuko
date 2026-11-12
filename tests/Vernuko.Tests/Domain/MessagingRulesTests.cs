using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Vernuko.Core;
using Vernuko.Messaging;

namespace Vernuko.Tests.Domain;

public class MessagingRulesTests
{
    private static readonly QuietHours Quiet = QuietHours.Default; // 21:00–09:00

    [Theory]
    [InlineData("20:59", false)]
    [InlineData("21:00", true)]
    [InlineData("23:30", true)]
    [InlineData("08:59", true)]
    [InlineData("09:00", false)]
    public void Quiet_hours_wrap_midnight(string time, bool quiet) =>
        Assert.Equal(quiet, Quiet.Contains(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Reminder_is_sent_the_day_before_at_the_same_time_outside_quiet_hours()
    {
        var start = new DateTime(2026, 11, 13, 14, 0, 0);
        Assert.Equal(new DateTime(2026, 11, 12, 14, 0, 0), ReminderSchedule.SendAtLocal(start, new DateTime(2026, 11, 12, 10, 0, 0), 24, Quiet));
    }

    [Fact]
    public void Morning_appointment_with_a_short_lead_is_reminded_the_evening_before_not_at_night()
    {
        // 09:15 appointment, 2 h before = 07:15 (quiet) → 09:00 would be < 30 min before → the evening before, 20:59.
        var start = new DateTime(2026, 11, 13, 9, 15, 0);
        Assert.Equal(new DateTime(2026, 11, 12, 20, 59, 0), ReminderSchedule.SendAtLocal(start, new DateTime(2026, 11, 12, 18, 0, 0), 2, Quiet));
    }

    [Fact]
    public void Late_evening_appointment_reminder_moves_to_the_morning()
    {
        // 21:30 appointment, 24 h before = 21:30 the day before (quiet) → 09:00 on the day.
        var start = new DateTime(2026, 11, 13, 21, 30, 0);
        Assert.Equal(new DateTime(2026, 11, 13, 9, 0, 0), ReminderSchedule.SendAtLocal(start, new DateTime(2026, 11, 12, 8, 0, 0), 24, Quiet));
    }

    [Fact]
    public void No_reminder_when_it_would_arrive_less_than_30_minutes_before()
    {
        var start = new DateTime(2026, 11, 12, 10, 20, 0);
        Assert.Null(ReminderSchedule.SendAtLocal(start, new DateTime(2026, 11, 12, 10, 0, 0), 24, Quiet));
    }

    [Fact]
    public void Sms_length_estimate_for_plain_turkish_and_unicode_text()
    {
        Assert.Equal(("GSM-7", 1, 11), SmsSegments.Estimate("Merhaba Ali"));
        Assert.Equal("Türkçe", SmsSegments.Estimate("Merhaba Ayşe, yarın randevunuz var.").Encoding);
        Assert.Equal(2, SmsSegments.Estimate(new string('a', 161)).Segments);
        Assert.Equal("UCS-2", SmsSegments.Estimate("Görüşmek üzere 😊").Encoding);
    }

    [Fact]
    public void Temporary_failures_back_off_and_give_up_after_five_attempts()
    {
        var now = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        var m = OutboxMessage.Create(MessageChannel.Sms, MessagePurpose.AppointmentReminder, null, null, PhoneNumber.Parse("0532 111 22 33").Value, "x", false, "k", now, now).Value;
        m.MarkAttemptFailed("p", "timeout", retryable: true, now);
        Assert.Equal(OutboxStatus.Pending, m.Status);
        Assert.Equal(now.AddMinutes(1), m.NextAttemptUtc);
        for (var i = 0; i < 4; i++)
        {
            m.MarkAttemptFailed("p", "timeout", retryable: true, now);
        }

        Assert.Equal(OutboxStatus.Failed, m.Status);
        var permanent = OutboxMessage.Create(MessageChannel.Sms, MessagePurpose.AppointmentReminder, null, null, PhoneNumber.Parse("0532 111 22 33").Value, "x", false, "k2", now, now).Value;
        permanent.MarkAttemptFailed("p", "invalid number", retryable: false, now);
        Assert.Equal(OutboxStatus.Failed, permanent.Status);
    }

    [Fact]
    public async Task Ileti_Merkezi_request_matches_the_documented_shape_and_codes_are_mapped()
    {
        var handler = new RecordingHandler("{\"response\":{\"status\":{\"code\":200,\"message\":\"İşlem başarılı\"},\"order\":{\"id\":\"312891245\"}}}");
        var transport = new IletiMerkeziSmsTransport(new HttpClient(handler), "KEY", "HASH", "ISIL");
        var ok = await transport.SendAsync(PhoneNumber.Parse("0532 111 22 33").Value, "Merhaba", [], false, CancellationToken.None);
        Assert.True(ok.Accepted);
        Assert.Equal("312891245", ok.ProviderMessageId);
        Assert.Equal(IletiMerkeziSmsTransport.SendUri, handler.Uri);
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("KEY", body["request"]!["authentication"]!["key"]!.GetValue<string>());
        Assert.Equal("ISIL", body["request"]!["order"]!["sender"]!.GetValue<string>());
        Assert.Equal("0", body["request"]!["order"]!["iys"]!.GetValue<string>());
        Assert.Equal("+905321112233", body["request"]!["order"]!["message"]!["receipents"]!["number"]![0]!.GetValue<string>()); // documented spelling

        var auth = await new IletiMerkeziSmsTransport(new HttpClient(new RecordingHandler("{\"response\":{\"status\":{\"code\":401,\"message\":\"Üyelik bilgileri hatalı\"}}}")), "K", "H", "S")
            .SendAsync(PhoneNumber.Parse("0532 111 22 33").Value, "x", [], false, CancellationToken.None);
        Assert.False(auth.Accepted);
        Assert.False(auth.IsRetryable);
        Assert.Contains("kimlik", auth.ErrorMessage);

        var down = await new IletiMerkeziSmsTransport(new HttpClient(new RecordingHandler("{\"response\":{\"status\":{\"code\":503,\"message\":\"x\"}}}")), "K", "H", "S")
            .SendAsync(PhoneNumber.Parse("0532 111 22 33").Value, "x", [], false, CancellationToken.None);
        Assert.True(down.IsRetryable);
    }

    [Fact]
    public async Task WhatsApp_template_request_matches_the_documented_shape_and_codes_are_mapped()
    {
        var handler = new RecordingHandler("{\"messaging_product\":\"whatsapp\",\"contacts\":[{\"input\":\"+905321112233\",\"wa_id\":\"905321112233\"}],\"messages\":[{\"id\":\"wamid.ABC\"}]}");
        var transport = new WhatsAppCloudTransport(new HttpClient(handler), "v27.0", "106540352242922", "TOKEN", "randevu_hatirlatma", "tr");
        var ok = await transport.SendAsync(PhoneNumber.Parse("0532 111 22 33").Value, "ignored", ["Ayşe", "13 Kasım 2026 Cuma", "14:00", "Işıl"], false, CancellationToken.None);
        Assert.True(ok.Accepted);
        Assert.Equal("wamid.ABC", ok.ProviderMessageId);
        Assert.Equal("https://graph.facebook.com/v27.0/106540352242922/messages", handler.Uri!.ToString());
        Assert.Equal("Bearer TOKEN", handler.Authorization);
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("template", body["type"]!.GetValue<string>());
        Assert.Equal("randevu_hatirlatma", body["template"]!["name"]!.GetValue<string>());
        Assert.Equal("Ayşe", body["template"]!["components"]![0]!["parameters"]![0]!["text"]!.GetValue<string>());

        var notOnWhatsApp = await new WhatsAppCloudTransport(new HttpClient(new RecordingHandler("{\"error\":{\"message\":\"x\",\"code\":131026}}", HttpStatusCode.BadRequest)), "v27.0", "1", "T", "t", "tr")
            .SendAsync(PhoneNumber.Parse("0532 111 22 33").Value, "x", [], false, CancellationToken.None);
        Assert.False(notOnWhatsApp.IsRetryable);
        var throttled = await new WhatsAppCloudTransport(new HttpClient(new RecordingHandler("{\"error\":{\"message\":\"(#130429) Rate limit hit\",\"code\":130429}}", HttpStatusCode.BadRequest)), "v27.0", "1", "T", "t", "tr")
            .SendAsync(PhoneNumber.Parse("0532 111 22 33").Value, "x", [], false, CancellationToken.None);
        Assert.True(throttled.IsRetryable);
    }

    private sealed class RecordingHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public string? Body { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
