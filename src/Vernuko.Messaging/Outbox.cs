using Vernuko.Core;

namespace Vernuko.Messaging;

public enum MessageChannel
{
    Sms,
    WhatsApp,
}

public enum OutboxStatus
{
    /// <summary>Waiting for its send time (or a retry).</summary>
    Pending,

    Sent,

    /// <summary>Permanently failed (invalid number, rejected, too many attempts).</summary>
    Failed,

    /// <summary>Not sent on purpose (monthly limit reached, customer opted out, too late).</summary>
    Skipped,

    /// <summary>No longer needed (appointment cancelled or moved before sending).</summary>
    Cancelled,
}

/// <summary>
/// One automatic message. Written in the same transaction as the decision to send it (outbox pattern), so a crash
/// can neither lose it nor send it twice: <see cref="IdempotencyKey"/> is unique (purpose + appointment + time +
/// channel). Message text is stored because it is what the customer receives; it contains only first name,
/// date/time and business name (data minimisation).
/// </summary>
public sealed class OutboxMessage : Entity
{
    public const int MaxAttempts = 5;

    private OutboxMessage()
    {
    }

    public MessageChannel Channel { get; private set; }

    public MessagePurpose Purpose { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? AppointmentId { get; private set; }

    /// <summary>E.164 recipient number.</summary>
    public string Recipient { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public bool IsCommercial { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public OutboxStatus Status { get; private set; }

    public DateTime NextAttemptUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public string? ProviderKey { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public int Segments { get; private set; }

    public string? LastError { get; private set; }

    public static Result<OutboxMessage> Create(MessageChannel channel, MessagePurpose purpose, Guid? customerId, Guid? appointmentId, PhoneNumber recipient, string text, bool isCommercial, string idempotencyKey, DateTime sendAtUtc, DateTime nowUtc)
    {
        Guard.Utc(sendAtUtc);
        Guard.Utc(nowUtc);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("outbox.text", "Mesaj metni boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Error.Validation("outbox.key", "Mesaj anahtarı gerekli.");
        }

        return new OutboxMessage
        {
            Channel = channel,
            Purpose = purpose,
            CustomerId = customerId,
            AppointmentId = appointmentId,
            Recipient = recipient.E164,
            Text = text.Trim(),
            IsCommercial = isCommercial,
            IdempotencyKey = idempotencyKey,
            Status = OutboxStatus.Pending,
            CreatedAtUtc = nowUtc,
            NextAttemptUtc = sendAtUtc,
            Segments = SmsSegments.Estimate(text).Segments,
        };
    }

    public bool IsDue(DateTime nowUtc) => Status == OutboxStatus.Pending && NextAttemptUtc <= nowUtc;

    public void MarkSent(string providerKey, string? providerMessageId, int? segments, DateTime nowUtc)
    {
        Status = OutboxStatus.Sent;
        Attempts++;
        SentAtUtc = nowUtc;
        ProviderKey = providerKey;
        ProviderMessageId = providerMessageId;
        if (segments is > 0)
        {
            Segments = segments.Value;
        }

        LastError = null;
    }

    /// <summary>Temporary errors back off (1, 5, 15, 60 min); permanent ones or the 5th failure end the message.</summary>
    public void MarkAttemptFailed(string providerKey, string error, bool retryable, DateTime nowUtc)
    {
        Attempts++;
        ProviderKey = providerKey;
        LastError = error;
        if (!retryable || Attempts >= MaxAttempts)
        {
            Status = OutboxStatus.Failed;
            return;
        }

        NextAttemptUtc = nowUtc + Backoff(Attempts);
    }

    public void Defer(DateTime untilUtc) => NextAttemptUtc = untilUtc;

    public void Skip(string reason)
    {
        Status = OutboxStatus.Skipped;
        LastError = reason;
    }

    public void Cancel(string reason)
    {
        if (Status == OutboxStatus.Pending)
        {
            Status = OutboxStatus.Cancelled;
            LastError = reason;
        }
    }

    /// <summary>KVKK erasure: the number and the text (first name, date) go; the row stays for the monthly count.</summary>
    public void Anonymize()
    {
        Cancel("Müşteri verisi anonimleştirildi.");
        Recipient = "***";
        Text = "[anonimleştirildi]";
    }

    public static TimeSpan Backoff(int attempts) => attempts switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromMinutes(60),
    };

    public static string StatusName(OutboxStatus status) => status switch
    {
        OutboxStatus.Pending => "Bekliyor",
        OutboxStatus.Sent => "Gönderildi",
        OutboxStatus.Failed => "Gönderilemedi",
        OutboxStatus.Skipped => "Gönderilmedi",
        _ => "İptal",
    };
}

/// <summary>
/// "Sessiz saatler": automatic messages are not sent late at night or early in the morning. Times are local
/// business time; the window may wrap midnight (21:00–09:00).
/// </summary>
public sealed record QuietHours(TimeOnly Start, TimeOnly End)
{
    public static readonly QuietHours Default = new(new TimeOnly(21, 0), new TimeOnly(9, 0));

    public bool Contains(TimeOnly t) => Start == End ? false : Start < End ? t >= Start && t < End : t >= Start || t < End;

    /// <summary>The first local moment at or after <paramref name="local"/> that is outside the quiet window.</summary>
    public DateTime NextAllowed(DateTime local)
    {
        if (!Contains(TimeOnly.FromDateTime(local)))
        {
            return local;
        }

        var end = local.Date + End.ToTimeSpan();
        return end > local ? end : end.AddDays(1);
    }
}

/// <summary>When to send an appointment reminder.</summary>
public static class ReminderSchedule
{
    /// <summary>A reminder must arrive at least this long before the appointment to be useful.</summary>
    public static readonly TimeSpan MinimumLead = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Ideal time is <paramref name="hoursBefore"/> before the start, never in quiet hours. If quiet hours would push
    /// it past the latest useful moment, it is sent just before the quiet window starts instead. Returns null when no
    /// useful time is left.
    /// </summary>
    public static DateTime? SendAtLocal(DateTime startLocal, DateTime nowLocal, int hoursBefore, QuietHours quiet)
    {
        var latest = startLocal - MinimumLead;
        var ideal = startLocal.AddHours(-hoursBefore);
        var candidate = ideal < nowLocal ? nowLocal : ideal;
        if (candidate > latest)
        {
            return null;
        }

        candidate = quiet.NextAllowed(candidate);
        if (candidate <= latest)
        {
            return candidate;
        }

        var before = LatestAllowedAtOrBefore(latest, quiet);
        return before >= nowLocal ? before : null;
    }

    private static DateTime LatestAllowedAtOrBefore(DateTime t, QuietHours quiet)
    {
        var time = TimeOnly.FromDateTime(t);
        if (!quiet.Contains(time))
        {
            return t;
        }

        var startToday = t.Date + quiet.Start.ToTimeSpan() - TimeSpan.FromMinutes(1);
        return quiet.Start < quiet.End || time >= quiet.Start ? startToday : startToday.AddDays(-1);
    }
}

/// <summary>
/// Rough SMS length estimate for the editor and the cost estimate. Turkish providers count differently from each
/// other (e.g. a mandatory operator code shortens one part to 155), so the provider's own count — when it returns
/// one — replaces this after sending.
/// </summary>
public static class SmsSegments
{
    private const string Gsm7Basic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Gsm7Extension = "^{}\\[~]|€";
    private const string TurkishShift = "ğĞıİşŞç";

    public static (string Encoding, int Segments, int Characters) Estimate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var gsm = true;
        var turkish = false;
        var units = 0;
        foreach (var ch in text)
        {
            if (Gsm7Basic.Contains(ch, StringComparison.Ordinal))
            {
                units++;
            }
            else if (Gsm7Extension.Contains(ch, StringComparison.Ordinal))
            {
                units += 2;
            }
            else if (TurkishShift.Contains(ch, StringComparison.Ordinal))
            {
                turkish = true;
                units++;
            }
            else
            {
                gsm = false;
            }
        }

        if (!gsm)
        {
            var len = text.Length;
            return ("UCS-2", len <= 70 ? 1 : (int)Math.Ceiling(len / 67.0), len);
        }

        if (turkish)
        {
            return ("Türkçe", units <= 155 ? 1 : (int)Math.Ceiling(units / 149.0), units);
        }

        return ("GSM-7", units <= 160 ? 1 : (int)Math.Ceiling(units / 153.0), units);
    }
}
