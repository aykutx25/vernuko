using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Messaging;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Reminder planning and sending with a scripted provider. Test clock: 12 Nov 2026 10:00 Istanbul.</summary>
public class AutoMessagingTests
{
    private static MessagingSettingsUpdate Settings(int limit = 10, bool enabled = true, MessageChannel channel = MessageChannel.Sms) =>
        new(enabled, channel, 24, "21:00", "09:00", limit, 30, "ISIL", "KEY", "HASH", "106540352242922", "TOKEN", "randevu_hatirlatma", "tr", null);

    private static async Task<(TestApp App, Guid Customer, Catalog Catalog)> ArrangeAsync()
    {
        var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync("Ayşe", "Kara", "0532 111 22 33");
        return (app, customer, await app.CatalogAsync());
    }

    private static async Task<Guid> BookAsync(TestApp app, Guid customer, Catalog c, DateTime local) =>
        (await app.Get<AppointmentService>().CreateAsync(new CreateAppointmentRequest(customer, c.Laser.Id, c.Selin.Id, [c.Room1.Id], local, null, null, null, null), confirmWarnings: true)).Value.AppointmentId!.Value;

    [Fact]
    public async Task Nothing_is_planned_or_sent_by_default()
    {
        var (app, customer, c) = await ArrangeAsync();
        await using var _ = app;
        await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 13));
        var svc = app.Get<AutoMessagingService>();
        Assert.Equal(0, await svc.PlanRemindersAsync());
        Assert.Equal(0, await svc.DispatchDueAsync());
        Assert.Empty(app.Get<FakeTransports>().Sent);
        var s = (await svc.GetSettingsAsync()).Value;
        Assert.False(s.RemindersEnabled);
        Assert.Equal(0, s.MonthlyLimit);
    }

    [Fact]
    public async Task Reminder_is_planned_once_sent_once_and_replanned_when_the_appointment_moves()
    {
        var (app, customer, c) = await ArrangeAsync();
        await using var _ = app;
        var svc = app.Get<AutoMessagingService>();
        Assert.True((await svc.SaveSettingsAsync(Settings())).IsSuccess);
        var appt = await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 13)); // 24 h before = now

        Assert.Equal(1, await svc.PlanRemindersAsync());
        Assert.Equal(0, await svc.PlanRemindersAsync()); // no duplicate
        Assert.Equal(1, await svc.DispatchDueAsync());
        Assert.Equal(0, await svc.DispatchDueAsync());
        var sent = Assert.Single(app.Get<FakeTransports>().Sent);
        Assert.Equal("+905321112233", sent.To);
        Assert.Contains("Ayşe", sent.Text);
        Assert.Contains("10:00", sent.Text);
        Assert.Contains("Işıl Güzellik ve Lazer", sent.Text);

        // Moved to the afternoon: a new reminder for the new time is planned (due tomorrow-minus-24h = today 15:00, not yet).
        var moved = await app.Get<AppointmentService>().RescheduleAsync(new RescheduleRequest(appt, TestApp.Local(15, 0, day: 13), c.Selin.Id, [c.Room1.Id]), confirmWarnings: true);
        Assert.True(moved.IsSuccess);
        Assert.Equal(1, await svc.PlanRemindersAsync());
        Assert.Equal(0, await svc.DispatchDueAsync());
        var outbox = (await svc.GetOutboxAsync()).Value;
        Assert.Contains(outbox, m => m.Status == OutboxStatus.Pending);
        Assert.Contains(outbox, m => m.Status == OutboxStatus.Sent && m.RecipientMasked == "+905*****2233");

        // Cancelled before the reminder time: the pending reminder is cancelled, never sent.
        app.Get<FakeDialogs>();
        Assert.True((await app.Get<AppointmentService>().ChangeStatusAsync(appt, AppointmentStatus.Cancelled, "Müşteri aradı")).IsSuccess);
        await svc.PlanRemindersAsync();
        Assert.Contains((await svc.GetOutboxAsync()).Value, m => m.Status == OutboxStatus.Cancelled);
    }

    [Fact]
    public async Task Monthly_limit_quiet_hours_and_opt_out_are_respected()
    {
        var (app, customer, c) = await ArrangeAsync();
        await using var _ = app;
        var svc = app.Get<AutoMessagingService>();
        var fake = app.Get<FakeTransports>();

        // Limit 1: the second due reminder is skipped, not sent.
        Assert.True((await svc.SaveSettingsAsync(Settings(limit: 1))).IsSuccess);
        var other = await app.AddCustomerAsync("Mehmet", "Demir", "0533 444 55 66");
        await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 13));
        await app.Get<AppointmentService>().CreateAsync(new CreateAppointmentRequest(other, c.Laser.Id, c.Derya.Id, [c.Room2.Id], TestApp.Local(10, 0, day: 13), null, null, null, null), confirmWarnings: true);
        Assert.Equal(2, await svc.PlanRemindersAsync());
        Assert.Equal(1, await svc.DispatchDueAsync());
        Assert.Single(fake.Sent);
        Assert.Contains((await svc.GetOutboxAsync()).Value, m => m.Status == OutboxStatus.Skipped && m.LastError!.Contains("limit", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, (await svc.GetMonthAsync()).Value.Sent);

        // Quiet hours: at 22:00 nothing goes out; the message waits until 09:00.
        app.Time.Now = new DateTimeOffset(2026, 11, 12, 19, 0, 0, TimeSpan.Zero); // 22:00 Istanbul
        Assert.True((await svc.SaveSettingsAsync(Settings(limit: 100))).IsSuccess);
        await BookAsync(app, customer, c, TestApp.Local(11, 0, day: 13));
        Assert.Equal(1, await svc.PlanRemindersAsync());
        Assert.Equal(0, await svc.DispatchDueAsync());
        var waiting = Assert.Single((await svc.GetOutboxAsync()).Value, m => m.Status == OutboxStatus.Pending);
        Assert.Equal(new DateTime(2026, 11, 13, 6, 0, 0, DateTimeKind.Utc), waiting.NextAttemptUtc); // 09:00 Istanbul

        // Customer withdrew SMS consent before sending: skipped.
        var details = (await app.Get<CustomerService>().GetAsync(customer)).Value;
        Assert.True((await app.Get<CustomerService>().UpdateAsync(customer, details.Version, new Customers.CustomerDetails(details.FirstName, details.LastName, details.Phone), allowSms: false, allowWhatsApp: true, allowEmail: true, marketingConsent: false)).IsSuccess);
        app.Time.Now = new DateTimeOffset(2026, 11, 13, 6, 5, 0, TimeSpan.Zero);
        Assert.Equal(0, await svc.DispatchDueAsync());
        Assert.Contains((await svc.GetOutboxAsync()).Value, m => m.Status == OutboxStatus.Skipped && m.LastError!.Contains("istemiyor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Provider_errors_retry_or_stop_and_whatsapp_gets_template_parameters()
    {
        var (app, customer, c) = await ArrangeAsync();
        await using var _ = app;
        var svc = app.Get<AutoMessagingService>();
        var fake = app.Get<FakeTransports>();
        Assert.True((await svc.SaveSettingsAsync(Settings(channel: MessageChannel.WhatsApp))).IsSuccess);
        await BookAsync(app, customer, c, TestApp.Local(10, 0, day: 13));
        await svc.PlanRemindersAsync();

        fake.NextOutcomes.Enqueue(SendOutcome.Fail("130429", "hız sınırı", retryable: true));
        Assert.Equal(0, await svc.DispatchDueAsync());
        var retry = Assert.Single((await svc.GetOutboxAsync()).Value);
        Assert.Equal(OutboxStatus.Pending, retry.Status);
        Assert.Equal(1, retry.Attempts);

        app.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await svc.DispatchDueAsync());
        var wa = Assert.Single(fake.Sent);
        Assert.Equal(MessageChannel.WhatsApp, wa.Channel);
        Assert.Equal(["Ayşe", "13 Kasım 2026 Cuma", "10:00", "Işıl Güzellik ve Lazer"], wa.Parameters);
    }

    [Fact]
    public async Task Credentials_are_stored_encrypted_and_test_message_counts_toward_the_limit()
    {
        var (app, _, _) = await ArrangeAsync();
        await using var __ = app;
        var svc = app.Get<AutoMessagingService>();
        Assert.True((await svc.SaveSettingsAsync(Settings(limit: 1))).IsSuccess);
        await using (var db = await app.DbAsync())
        {
            var raw = await Vernuko.Application.Common.SettingsStore.GetAsync(db, "messaging.sms.hash", CancellationToken.None);
            Assert.NotNull(raw);
            Assert.DoesNotContain("HASH", raw, StringComparison.Ordinal);
        }

        // Saving again without secrets keeps them.
        Assert.True((await svc.SaveSettingsAsync(Settings(limit: 1) with { SmsKey = null, SmsHash = null })).IsSuccess);
        Assert.True((await svc.GetSettingsAsync()).Value.SmsConfigured);

        Assert.True((await svc.SendTestAsync(MessageChannel.Sms, "0532 999 88 77")).IsSuccess);
        Assert.Equal("Vernuko deneme mesajı - Işıl Güzellik ve Lazer", app.Get<FakeTransports>().Sent.Single().Text);
        var second = await svc.SendTestAsync(MessageChannel.Sms, "0532 999 88 77");
        Assert.Equal("validation.messaging.limit_reached", second.Error.Code);
    }
}
