using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vernuko.Appointments;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Messaging;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed record MessagingSettings(
    bool RemindersEnabled,
    MessageChannel ReminderChannel,
    int ReminderHoursBefore,
    TimeOnly QuietStart,
    TimeOnly QuietEnd,
    int MonthlyLimit,
    long SmsUnitPriceMinor,
    string? SmsSender,
    bool SmsCredentialsSaved,
    string? WhatsAppPhoneNumberId,
    bool WhatsAppTokenSaved,
    string? WhatsAppTemplate,
    string WhatsAppLanguage,
    string WhatsAppApiVersion)
{
    public bool SmsConfigured => SmsCredentialsSaved && !string.IsNullOrWhiteSpace(SmsSender);

    public bool WhatsAppConfigured => WhatsAppTokenSaved && !string.IsNullOrWhiteSpace(WhatsAppPhoneNumberId) && !string.IsNullOrWhiteSpace(WhatsAppTemplate);

    public bool IsConfigured(MessageChannel channel) => channel == MessageChannel.Sms ? SmsConfigured : WhatsAppConfigured;

    public QuietHours Quiet => new(QuietStart, QuietEnd);
}

/// <summary>Secrets are optional: null or empty keeps the saved value.</summary>
public sealed record MessagingSettingsUpdate(
    bool RemindersEnabled,
    MessageChannel ReminderChannel,
    int ReminderHoursBefore,
    string QuietStart,
    string QuietEnd,
    int MonthlyLimit,
    long SmsUnitPriceMinor,
    string? SmsSender,
    string? SmsKey,
    string? SmsHash,
    string? WhatsAppPhoneNumberId,
    string? WhatsAppToken,
    string? WhatsAppTemplate,
    string? WhatsAppLanguage,
    string? WhatsAppApiVersion);

public sealed record OutboxItem(Guid Id, DateTime CreatedAtUtc, DateTime? SentAtUtc, DateTime NextAttemptUtc, MessageChannel Channel, MessagePurpose Purpose, string RecipientMasked, OutboxStatus Status, int Attempts, int Segments, string? LastError)
{
    public string StatusName => OutboxMessage.StatusName(Status);

    public string ChannelName => Channel == MessageChannel.Sms ? "SMS" : "WhatsApp";

    public string PurposeName => Purpose switch
    {
        MessagePurpose.AppointmentReminder => "Randevu hatırlatma",
        MessagePurpose.Test => "Deneme",
        _ => Purpose.ToString(),
    };
}

public sealed record MessagingMonth(int Sent, int Failed, int Pending, int Limit, long EstimatedCostMinor);

public sealed record TransportCredentials(string? SmsSender, string? SmsKey, string? SmsHash, string? WhatsAppPhoneNumberId, string? WhatsAppToken, string? WhatsAppTemplate, string WhatsAppLanguage, string WhatsAppApiVersion);

public interface IMessageTransportFactory
{
    IMessageTransport? Create(MessageChannel channel, TransportCredentials credentials);
}

/// <summary>Creates the real HTTP transports (İleti Merkezi SMS, WhatsApp Cloud API).</summary>
public sealed class HttpMessageTransportFactory(HttpClient http) : IMessageTransportFactory
{
    public IMessageTransport? Create(MessageChannel channel, TransportCredentials credentials) => channel switch
    {
        MessageChannel.Sms when credentials is { SmsKey: { Length: > 0 } key, SmsHash: { Length: > 0 } hash, SmsSender: { Length: > 0 } sender } =>
            new IletiMerkeziSmsTransport(http, key, hash, sender),
        MessageChannel.WhatsApp when credentials is { WhatsAppToken: { Length: > 0 } token, WhatsAppPhoneNumberId: { Length: > 0 } phoneId, WhatsAppTemplate: { Length: > 0 } template } =>
            new WhatsAppCloudTransport(http, credentials.WhatsAppApiVersion, phoneId, token, template, credentials.WhatsAppLanguage),
        _ => null,
    };
}

/// <summary>
/// Automatic appointment reminders (Phase 5). Off by default; the monthly message limit starts at 0, so nothing is
/// sent — and nothing is charged — until the business enables reminders, connects its own provider account and
/// raises the limit. Runs only while Vernuko is open: the planner writes reminders to the outbox (one per
/// appointment time and channel, unique key), the dispatcher sends due messages outside quiet hours.
/// </summary>
public sealed class AutoMessagingService(
    AppServiceDeps deps,
    ISecretProtector protector,
    IMessageTransportFactory transports,
    ILicenseGate license,
    ILogger<AutoMessagingService> logger) : AppServiceBase(deps), IAutoMessagingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private const string KRemindersEnabled = "messaging.reminders_enabled";
    private const string KChannel = "messaging.reminder_channel";
    private const string KHours = "messaging.reminder_hours";
    private const string KQuietStart = "messaging.quiet_start";
    private const string KQuietEnd = "messaging.quiet_end";
    private const string KLimit = "messaging.monthly_limit";
    private const string KSmsPrice = "messaging.sms_unit_price_minor";
    private const string KSmsSender = "messaging.sms.sender";
    private const string KSmsKey = "messaging.sms.key";
    private const string KSmsHash = "messaging.sms.hash";
    private const string KWaPhone = "messaging.wa.phone_number_id";
    private const string KWaToken = "messaging.wa.token";
    private const string KWaTemplate = "messaging.wa.template";
    private const string KWaLanguage = "messaging.wa.language";
    private const string KWaVersion = "messaging.wa.api_version";

    public const string DefaultWhatsAppApiVersion = "v27.0";

    public Task<Result<MessagingSettings>> GetSettingsAsync(CancellationToken ct = default) =>
        ReadAsync<MessagingSettings>(Permission.SettingsManage, async db => await LoadSettingsAsync(db, ct), ct);

    public Task<Result<bool>> SaveSettingsAsync(MessagingSettingsUpdate u, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.SettingsManage, async db =>
        {
            if (u.ReminderHoursBefore is < 1 or > 72)
            {
                return Error.Validation("messaging.hours", "Hatırlatma 1 ile 72 saat önce gönderilebilir.");
            }

            if (u.MonthlyLimit is < 0 or > 100_000)
            {
                return Error.Validation("messaging.limit", "Aylık mesaj limiti 0 ile 100.000 arasında olmalıdır.");
            }

            if (!TimeOnly.TryParseExact(u.QuietStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var qs)
                || !TimeOnly.TryParseExact(u.QuietEnd, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var qe))
            {
                return Error.Validation("messaging.quiet", "Sessiz saatleri SS:DD biçiminde girin.");
            }

            if (u.SmsSender is { Length: > 11 })
            {
                return Error.Validation("messaging.sender", "SMS başlığı en fazla 11 karakter olabilir.");
            }

            var now = UtcNow;
            await SettingsStore.SetAsync(db, KRemindersEnabled, u.RemindersEnabled ? "true" : "false", now, ct);
            await SettingsStore.SetAsync(db, KChannel, u.ReminderChannel.ToString(), now, ct);
            await SettingsStore.SetAsync(db, KHours, u.ReminderHoursBefore.ToString(CultureInfo.InvariantCulture), now, ct);
            await SettingsStore.SetAsync(db, KQuietStart, qs.ToString("HH:mm", CultureInfo.InvariantCulture), now, ct);
            await SettingsStore.SetAsync(db, KQuietEnd, qe.ToString("HH:mm", CultureInfo.InvariantCulture), now, ct);
            await SettingsStore.SetAsync(db, KLimit, u.MonthlyLimit.ToString(CultureInfo.InvariantCulture), now, ct);
            await SettingsStore.SetAsync(db, KSmsPrice, Math.Max(0, u.SmsUnitPriceMinor).ToString(CultureInfo.InvariantCulture), now, ct);
            await SettingsStore.SetAsync(db, KSmsSender, u.SmsSender?.Trim() ?? string.Empty, now, ct);
            await SettingsStore.SetAsync(db, KWaPhone, u.WhatsAppPhoneNumberId?.Trim() ?? string.Empty, now, ct);
            await SettingsStore.SetAsync(db, KWaTemplate, u.WhatsAppTemplate?.Trim() ?? string.Empty, now, ct);
            await SettingsStore.SetAsync(db, KWaLanguage, string.IsNullOrWhiteSpace(u.WhatsAppLanguage) ? "tr" : u.WhatsAppLanguage.Trim(), now, ct);
            await SettingsStore.SetAsync(db, KWaVersion, string.IsNullOrWhiteSpace(u.WhatsAppApiVersion) ? DefaultWhatsAppApiVersion : u.WhatsAppApiVersion.Trim(), now, ct);
            await SaveSecretAsync(db, KSmsKey, u.SmsKey, now, ct);
            await SaveSecretAsync(db, KSmsHash, u.SmsHash, now, ct);
            await SaveSecretAsync(db, KWaToken, u.WhatsAppToken, now, ct);
            await Audit.AppendAsync(db, "messaging.configured", "Messaging", null,
                $"Otomatik hatırlatma {(u.RemindersEnabled ? "açık" : "kapalı")}, kanal {u.ReminderChannel}, {u.ReminderHoursBefore} saat önce, aylık limit {u.MonthlyLimit}", ct);
            return true;
        }, ct);

    /// <summary>Sends one message right now to check the account (counts toward the monthly limit).</summary>
    public async Task<Result<string>> SendTestAsync(MessageChannel channel, string phone, CancellationToken ct = default)
    {
        var auth = Authorize(Permission.SettingsManage);
        if (auth.IsFailure)
        {
            return auth.Error;
        }

        var to = PhoneNumber.Parse(phone);
        if (to.IsFailure)
        {
            return to.Error;
        }

        await using var db = await Factory.CreateDbContextAsync(ct);
        var settings = await LoadSettingsAsync(db, ct);
        var transport = transports.Create(channel, await LoadCredentialsAsync(db, ct));
        if (transport is null)
        {
            return Error.Validation("messaging.not_configured", $"{(channel == MessageChannel.Sms ? "SMS" : "WhatsApp")} hesabı bilgileri eksik.");
        }

        var (_, zone) = await BusinessContext.LoadAsync(db, ct);
        if (await SentThisMonthAsync(db, zone, ct) >= settings.MonthlyLimit)
        {
            return Error.Validation("messaging.limit_reached", $"Aylık mesaj limiti ({settings.MonthlyLimit}) dolu. Deneme için limiti artırın.");
        }

        var business = (await BusinessContext.LoadAsync(db, ct)).Settings;
        var nowLocal = TimeZones.ToLocal(UtcNow, zone);
        var text = $"Vernuko deneme mesajı - {business.BusinessName}";
        var message = OutboxMessage.Create(channel, MessagePurpose.Test, null, null, to.Value, text, false, "test:" + Ids.New().ToString("N"), UtcNow, UtcNow).Value;
        var outcome = await transport.SendAsync(to.Value, text, ["Deneme", nowLocal.ToString("d MMMM yyyy dddd", TurkishText.Culture), nowLocal.ToString("HH:mm", TurkishText.Culture), business.BusinessName], false, ct);
        if (outcome.Accepted)
        {
            message.MarkSent(transport.ProviderKey, outcome.ProviderMessageId, null, UtcNow);
        }
        else
        {
            message.MarkAttemptFailed(transport.ProviderKey, outcome.ErrorMessage ?? "Bilinmeyen hata", retryable: false, UtcNow);
        }

        db.Outbox.Add(message);
        await db.SaveChangesAsync(ct);
        return outcome.Accepted
            ? $"Deneme mesajı sağlayıcıya iletildi (numara {Mask(to.Value.E164)}). Telefonunuza ulaştığını kontrol edin."
            : Error.Validation("messaging.test_failed", outcome.ErrorMessage ?? "Mesaj gönderilemedi.");
    }

    public Task<Result<IReadOnlyList<OutboxItem>>> GetOutboxAsync(int take = 100, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<OutboxItem>>(Permission.MessagingSend, async db =>
            (await db.Outbox.AsNoTracking().OrderByDescending(m => m.CreatedAtUtc).Take(take).ToListAsync(ct))
                .Select(m => new OutboxItem(m.Id, m.CreatedAtUtc, m.SentAtUtc, m.NextAttemptUtc, m.Channel, m.Purpose, Mask(m.Recipient), m.Status, m.Attempts, m.Segments, m.LastError))
                .ToList(), ct);

    public Task<Result<MessagingMonth>> GetMonthAsync(CancellationToken ct = default) =>
        ReadAsync<MessagingMonth>(Permission.MessagingSend, async db =>
        {
            var settings = await LoadSettingsAsync(db, ct);
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = MonthStartUtc(zone);
            var month = await db.Outbox.AsNoTracking().Where(m => m.CreatedAtUtc >= start || (m.SentAtUtc != null && m.SentAtUtc >= start)).ToListAsync(ct);
            var sent = month.Where(m => m.Status == OutboxStatus.Sent && m.SentAtUtc >= start).ToList();
            var smsSegments = sent.Where(m => m.Channel == MessageChannel.Sms).Sum(m => m.Segments);
            return new MessagingMonth(sent.Count, month.Count(m => m.Status == OutboxStatus.Failed), month.Count(m => m.Status == OutboxStatus.Pending), settings.MonthlyLimit, smsSegments * settings.SmsUnitPriceMinor);
        }, ct);

    /// <summary>Background (no user needed): writes due reminders to the outbox and cancels stale ones.</summary>
    public async Task<int> PlanRemindersAsync(CancellationToken ct = default)
    {
        await using var db = await Factory.CreateDbContextAsync(ct);
        if (!await db.BusinessSettings.AnyAsync(s => s.SetupCompletedAtUtc != null, ct))
        {
            return 0;
        }

        var settings = await LoadSettingsAsync(db, ct);
        var (business, zone) = await BusinessContext.LoadAsync(db, ct);
        var now = UtcNow;
        var created = 0;

        // Cancel pending reminders whose appointment was cancelled, completed or moved.
        var pending = await db.Outbox.Where(m => m.Status == OutboxStatus.Pending && m.Purpose == MessagePurpose.AppointmentReminder && m.AppointmentId != null).ToListAsync(ct);
        var ids = pending.Select(m => m.AppointmentId!.Value).Distinct().ToList();
        var current = await db.Appointments.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        foreach (var m in pending)
        {
            if (!current.TryGetValue(m.AppointmentId!.Value, out var a) || !IsRemindable(a.Status) || m.IdempotencyKey != ReminderKey(a, m.Channel) || !settings.RemindersEnabled)
            {
                m.Cancel(settings.RemindersEnabled ? "Randevu iptal edildi, tamamlandı veya taşındı." : "Otomatik hatırlatma kapatıldı.");
            }
        }

        if (settings.RemindersEnabled && settings.IsConfigured(settings.ReminderChannel))
        {
            var horizon = now.AddHours(settings.ReminderHoursBefore + 24);
            var open = new[] { AppointmentStatus.Created, AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed };
            var appointments = await db.Appointments.AsNoTracking().Where(a => a.StartUtc > now && a.StartUtc <= horizon && open.Contains(a.Status)).ToListAsync(ct);
            var keys = appointments.Select(a => ReminderKey(a, settings.ReminderChannel)).ToList();
            var existing = (await db.Outbox.AsNoTracking().Where(m => keys.Contains(m.IdempotencyKey)).Select(m => m.IdempotencyKey).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
            var nowLocal = TimeZones.ToLocal(now, zone);
            foreach (var a in appointments)
            {
                var key = ReminderKey(a, settings.ReminderChannel);
                if (existing.Contains(key))
                {
                    continue;
                }

                var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == a.CustomerId, ct);
                if (customer is null || customer.State != CustomerState.Active || customer.Phone is null || !Allows(customer, settings.ReminderChannel)
                    || PhoneNumber.Parse(customer.Phone) is not { IsSuccess: true } phone)
                {
                    continue;
                }

                var startLocal = TimeZones.ToLocal(a.StartUtc, zone);
                if (ReminderSchedule.SendAtLocal(startLocal, nowLocal, settings.ReminderHoursBefore, settings.Quiet) is not { } sendLocal)
                {
                    continue;
                }

                var service = await db.Services.AsNoTracking().Where(s => s.Id == a.ServiceId).Select(s => s.Name).FirstOrDefaultAsync(ct);
                var staff = await db.Staff.AsNoTracking().Where(s => s.Id == a.StaffMemberId).Select(s => s.DisplayName).FirstOrDefaultAsync(ct);
                var text = MessageTemplates.Render(MessageTemplates.Defaults[MessagePurpose.AppointmentReminder],
                    new MessageContext(customer.FirstName, business.BusinessName, startLocal, service, staff, business.Phone), business.Profile.HideServiceNameInMessages);
                var message = OutboxMessage.Create(settings.ReminderChannel, MessagePurpose.AppointmentReminder, customer.Id, a.Id, phone.Value, text, false, key, TimeZones.ToUtc(sendLocal, zone), now);
                if (message.IsSuccess)
                {
                    db.Outbox.Add(message.Value);
                    created++;
                }
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Another planner run won the race on the unique key; nothing is lost or doubled.
            logger.LogInformation(ex, "Reminder planning collided with a concurrent run");
            return 0;
        }

        return created;
    }

    /// <summary>Background: sends due messages, respecting quiet hours, the monthly limit and opt-outs.</summary>
    public async Task<int> DispatchDueAsync(CancellationToken ct = default)
    {
        if (!await Gate.WaitAsync(0, ct))
        {
            return 0;
        }

        try
        {
            // Read-only license: no automatic sending (it costs the business money); messages simply wait.
            if (!license.CanWrite)
            {
                return 0;
            }

            await using var db = await Factory.CreateDbContextAsync(ct);
            if (!await db.BusinessSettings.AnyAsync(s => s.SetupCompletedAtUtc != null, ct))
            {
                return 0;
            }

            var settings = await LoadSettingsAsync(db, ct);
            var credentials = await LoadCredentialsAsync(db, ct);
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var now = UtcNow;
            var due = await db.Outbox.Where(m => m.Status == OutboxStatus.Pending && m.NextAttemptUtc <= now).OrderBy(m => m.NextAttemptUtc).Take(50).ToListAsync(ct);
            var sent = 0;
            foreach (var m in due)
            {
                var nowLocal = TimeZones.ToLocal(now, zone);
                if (settings.Quiet.Contains(TimeOnly.FromDateTime(nowLocal)))
                {
                    m.Defer(TimeZones.ToUtc(settings.Quiet.NextAllowed(nowLocal), zone));
                    continue;
                }

                if (await SentThisMonthAsync(db, zone, ct) >= settings.MonthlyLimit)
                {
                    m.Skip($"Aylık mesaj limiti ({settings.MonthlyLimit}) doldu. Ayarlar › Mesajlaşma'dan artırılabilir.");
                    continue;
                }

                if (m.AppointmentId is { } apptId)
                {
                    var appt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == apptId, ct);
                    if (appt is null || !IsRemindable(appt.Status) || m.IdempotencyKey != ReminderKey(appt, m.Channel))
                    {
                        m.Cancel("Randevu iptal edildi, tamamlandı veya taşındı.");
                        continue;
                    }

                    if (appt.StartUtc <= now)
                    {
                        m.Skip("Randevu saati geçti.");
                        continue;
                    }
                }

                if (m.CustomerId is { } customerId)
                {
                    var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
                    if (customer is null || customer.State != CustomerState.Active || !Allows(customer, m.Channel))
                    {
                        m.Skip("Müşteri bu kanaldan mesaj almak istemiyor veya kaydı arşivlendi.");
                        continue;
                    }
                }

                var transport = transports.Create(m.Channel, credentials);
                if (transport is null)
                {
                    m.Skip("Mesaj kanalının hesap bilgileri eksik.");
                    continue;
                }

                // Claim first so a crash right after the provider accepted it does not resend immediately.
                m.Defer(now.AddMinutes(10));
                await db.SaveChangesAsync(ct);

                var parameters = await TemplateParametersAsync(db, m, zone, ct);
                var outcome = await transport.SendAsync(PhoneNumber.Parse(m.Recipient).Value, m.Text, parameters, m.IsCommercial, ct);
                if (outcome.Accepted)
                {
                    m.MarkSent(transport.ProviderKey, outcome.ProviderMessageId, null, UtcNow);
                    sent++;
                }
                else
                {
                    m.MarkAttemptFailed(transport.ProviderKey, outcome.ErrorMessage ?? outcome.ErrorCode ?? "Bilinmeyen hata", outcome.IsRetryable, UtcNow);
                }

                await db.SaveChangesAsync(ct);
            }

            await db.SaveChangesAsync(ct);
            return sent;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static string ReminderKey(Appointment a, MessageChannel channel) =>
        $"reminder:{a.Id:N}:{a.StartUtc.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture)}:{channel}";

    /// <summary>"+90 5** *** 45 67" — enough to recognise, not enough to copy.</summary>
    public static string Mask(string e164)
    {
        if (e164.Length < 6)
        {
            return "***";
        }

        var sb = new StringBuilder();
        for (var i = 0; i < e164.Length; i++)
        {
            sb.Append(i < 4 || i >= e164.Length - 4 ? e164[i] : '*');
        }

        return sb.ToString();
    }

    private static bool IsRemindable(AppointmentStatus s) => s is AppointmentStatus.Created or AppointmentStatus.PendingConfirmation or AppointmentStatus.Confirmed;

    private static bool Allows(Customer c, MessageChannel channel) => channel == MessageChannel.Sms ? c.AllowSms : c.AllowWhatsApp;

    private DateTime MonthStartUtc(TimeZoneInfo zone)
    {
        var local = TimeZones.ToLocal(UtcNow, zone);
        return TimeZones.ToUtc(new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified), zone);
    }

    private async Task<int> SentThisMonthAsync(VernukoDbContext db, TimeZoneInfo zone, CancellationToken ct)
    {
        var start = MonthStartUtc(zone);
        return await db.Outbox.AsNoTracking().CountAsync(m => m.Status == OutboxStatus.Sent && m.SentAtUtc >= start, ct);
    }

    /// <summary>WhatsApp template body {{1}} first name, {{2}} date, {{3}} time, {{4}} business name.</summary>
    private static async Task<IReadOnlyList<string>> TemplateParametersAsync(VernukoDbContext db, OutboxMessage m, TimeZoneInfo zone, CancellationToken ct)
    {
        if (m.AppointmentId is not { } id)
        {
            return [];
        }

        var appt = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == id, ct);
        var first = await db.Customers.AsNoTracking().Where(c => c.Id == appt.CustomerId).Select(c => c.FirstName).FirstAsync(ct);
        var business = await db.BusinessSettings.AsNoTracking().Select(s => s.BusinessName).FirstAsync(ct);
        var local = TimeZones.ToLocal(appt.StartUtc, zone);
        return [first, local.ToString("d MMMM yyyy dddd", TurkishText.Culture), local.ToString("HH:mm", TurkishText.Culture), business];
    }

    private async Task<MessagingSettings> LoadSettingsAsync(VernukoDbContext db, CancellationToken ct)
    {
        async Task<string?> Get(string key) => await SettingsStore.GetAsync(db, key, ct) is { Length: > 0 } v ? v : null;
        static TimeOnly Time(string? v, TimeOnly fallback) => TimeOnly.TryParseExact(v, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : fallback;
        static int Int(string? v, int fallback) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;
        return new MessagingSettings(
            await Get(KRemindersEnabled) == "true",
            Enum.TryParse<MessageChannel>(await Get(KChannel), out var ch) ? ch : MessageChannel.Sms,
            Int(await Get(KHours), 24),
            Time(await Get(KQuietStart), QuietHours.Default.Start),
            Time(await Get(KQuietEnd), QuietHours.Default.End),
            Int(await Get(KLimit), 0),
            long.TryParse(await Get(KSmsPrice), NumberStyles.Integer, CultureInfo.InvariantCulture, out var price) ? price : 0,
            await Get(KSmsSender),
            await Get(KSmsKey) is not null && await Get(KSmsHash) is not null,
            await Get(KWaPhone),
            await Get(KWaToken) is not null,
            await Get(KWaTemplate),
            await Get(KWaLanguage) ?? "tr",
            await Get(KWaVersion) ?? DefaultWhatsAppApiVersion);
    }

    private async Task<TransportCredentials> LoadCredentialsAsync(VernukoDbContext db, CancellationToken ct)
    {
        var s = await LoadSettingsAsync(db, ct);
        return new TransportCredentials(s.SmsSender, await ReadSecretAsync(db, KSmsKey, ct), await ReadSecretAsync(db, KSmsHash, ct),
            s.WhatsAppPhoneNumberId, await ReadSecretAsync(db, KWaToken, ct), s.WhatsAppTemplate, s.WhatsAppLanguage, s.WhatsAppApiVersion);
    }

    private async Task SaveSecretAsync(VernukoDbContext db, string key, string? value, DateTime now, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            await SettingsStore.SetAsync(db, key, Convert.ToBase64String(protector.Protect(Encoding.UTF8.GetBytes(value.Trim()))), now, ct);
        }
    }

    private async Task<string?> ReadSecretAsync(VernukoDbContext db, string key, CancellationToken ct)
    {
        var stored = await SettingsStore.GetAsync(db, key, ct);
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(protector.Unprotect(Convert.FromBase64String(stored)));
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            // Saved on another computer / Windows account: the user has to enter the provider credentials again.
            logger.LogWarning(ex, "Messaging credential {Key} cannot be decrypted on this computer", key);
            return null;
        }
    }
}
