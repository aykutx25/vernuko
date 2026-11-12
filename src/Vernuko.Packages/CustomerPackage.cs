using Vernuko.Core;

namespace Vernuko.Packages;

public enum PackageStatus
{
    Active,
    Frozen,
    Cancelled,
}

public enum SessionEntryKind
{
    /// <summary>A completed appointment used one session.</summary>
    Consumed,

    /// <summary>A consumption was reversed (completed appointment re-opened).</summary>
    Restored,

    /// <summary>Authorised manual correction with a mandatory reason.</summary>
    Adjustment,
}

/// <summary>
/// Append-only session ledger. Remaining sessions are always derived from these rows, never stored as a
/// mutable counter, so every change is traceable (who, when, why, which appointment).
/// </summary>
public sealed class SessionLedgerEntry
{
    private SessionLedgerEntry()
    {
    }

    internal SessionLedgerEntry(Guid packageId, SessionEntryKind kind, int delta, Guid? appointmentId, string? reason, Guid? userId, DateTime atUtc)
    {
        Id = Ids.New();
        CustomerPackageId = packageId;
        Kind = kind;
        Delta = delta;
        AppointmentId = appointmentId;
        Reason = reason;
        UserId = userId;
        AtUtc = atUtc;
    }

    public Guid Id { get; private set; }

    public Guid CustomerPackageId { get; private set; }

    public SessionEntryKind Kind { get; private set; }

    /// <summary>Effect on remaining sessions: Consumed = -1, Restored = +1, Adjustment = ±n.</summary>
    public int Delta { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public string? Reason { get; private set; }

    public Guid? UserId { get; private set; }

    public DateTime AtUtc { get; private set; }
}

/// <summary>A package bought by a customer (e.g. 8 laser sessions for the legs).</summary>
public sealed class CustomerPackage : Entity
{
    private readonly List<SessionLedgerEntry> _entries = [];

    private CustomerPackage()
    {
    }

    private CustomerPackage(Guid customerId, Guid serviceId, Guid? templateId, string name, string? bodyArea, int totalSessions, long priceMinor, DateTime purchasedAtUtc, DateTime? expiresAtUtc)
    {
        CustomerId = customerId;
        ServiceId = serviceId;
        TemplateId = templateId;
        Name = name;
        BodyArea = bodyArea;
        TotalSessions = totalSessions;
        PriceMinor = priceMinor;
        PurchasedAtUtc = purchasedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = PackageStatus.Active;
    }

    public Guid CustomerId { get; private set; }

    public Guid ServiceId { get; private set; }

    public Guid? TemplateId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? BodyArea { get; private set; }

    public int TotalSessions { get; private set; }

    public long PriceMinor { get; private set; }

    public DateTime PurchasedAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public PackageStatus Status { get; private set; }

    public DateTime? FrozenAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<SessionLedgerEntry> Entries => _entries;

    public int RemainingSessions => TotalSessions + _entries.Sum(e => e.Delta);

    public int UsedSessions =>
        _entries.Where(e => e.Kind == SessionEntryKind.Consumed).Sum(e => -e.Delta)
        - _entries.Where(e => e.Kind == SessionEntryKind.Restored).Sum(e => e.Delta);

    public static Result<CustomerPackage> Sell(
        Guid customerId,
        Guid serviceId,
        Guid? templateId,
        string name,
        string? bodyArea,
        int totalSessions,
        long priceMinor,
        int? validityDays,
        DateTime nowUtc)
    {
        Guard.Utc(nowUtc);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("package.name", "Paket adı zorunludur.");
        }

        if (totalSessions is < 1 or > 500)
        {
            return Error.Validation("package.sessions", "Seans sayısı 1 ile 500 arasında olmalıdır.");
        }

        if (priceMinor < 0)
        {
            return Error.Validation("package.price", "Paket fiyatı negatif olamaz.");
        }

        DateTime? expires = validityDays is { } days ? nowUtc.AddDays(days) : null;
        return new CustomerPackage(customerId, serviceId, templateId, name.Trim(), string.IsNullOrWhiteSpace(bodyArea) ? null : bodyArea.Trim(), totalSessions, priceMinor, nowUtc, expires);
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc is { } exp && nowUtc >= exp && Status != PackageStatus.Frozen;

    /// <summary>Net sessions consumed for one appointment (0 or 1).</summary>
    public int NetConsumedFor(Guid appointmentId) =>
        -_entries.Where(e => e.AppointmentId == appointmentId && e.Kind != SessionEntryKind.Adjustment).Sum(e => e.Delta);

    public Result CanBook(DateTime nowUtc)
    {
        return Status switch
        {
            PackageStatus.Cancelled => Error.Validation("package.cancelled", "Bu paket iptal edilmiş."),
            PackageStatus.Frozen => Error.Validation("package.frozen", "Bu paket dondurulmuş. Önce paketi aktifleştirin."),
            _ when IsExpired(nowUtc) => Error.Validation("package.expired", "Bu paketin geçerlilik süresi dolmuş."),
            _ when RemainingSessions <= 0 => Error.Validation("package.no_sessions", "Bu pakette kalan seans yok."),
            _ => Result.Success(),
        };
    }

    /// <summary>
    /// Consumes one session for a completed appointment. Idempotent per appointment: completing the same
    /// appointment twice never consumes two sessions.
    /// </summary>
    public Result ConsumeFor(Guid appointmentId, Guid? userId, DateTime nowUtc)
    {
        if (NetConsumedFor(appointmentId) > 0)
        {
            return Result.Success();
        }

        if (Status == PackageStatus.Cancelled)
        {
            return Error.Validation("package.cancelled", "Bu paket iptal edilmiş.");
        }

        if (RemainingSessions <= 0)
        {
            return Error.Validation("package.no_sessions", "Bu pakette kalan seans yok. Seans düşülemedi.");
        }

        _entries.Add(new SessionLedgerEntry(Id, SessionEntryKind.Consumed, -1, appointmentId, null, userId, Guard.Utc(nowUtc)));
        return Result.Success();
    }

    /// <summary>Gives back the session of an appointment whose completion was reversed.</summary>
    public Result RestoreFor(Guid appointmentId, Guid? userId, DateTime nowUtc, string reason)
    {
        if (NetConsumedFor(appointmentId) <= 0)
        {
            return Result.Success();
        }

        _entries.Add(new SessionLedgerEntry(Id, SessionEntryKind.Restored, +1, appointmentId, reason, userId, Guard.Utc(nowUtc)));
        return Result.Success();
    }

    /// <summary>Authorised correction (requires PackagesAdjustSessions permission and a reason).</summary>
    public Result Adjust(int delta, string reason, Guid? userId, DateTime nowUtc)
    {
        if (delta == 0)
        {
            return Error.Validation("package.adjust_zero", "Düzeltme miktarı sıfır olamaz.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("package.adjust_reason", "Seans düzeltmesi için açıklama zorunludur.");
        }

        if (RemainingSessions + delta < 0)
        {
            return Error.Validation("package.adjust_negative", "Kalan seans sayısı sıfırın altına düşemez.");
        }

        _entries.Add(new SessionLedgerEntry(Id, SessionEntryKind.Adjustment, delta, null, reason.Trim(), userId, Guard.Utc(nowUtc)));
        return Result.Success();
    }

    public Result Freeze(DateTime nowUtc)
    {
        if (Status != PackageStatus.Active)
        {
            return Error.Validation("package.freeze", "Yalnızca aktif paketler dondurulabilir.");
        }

        Status = PackageStatus.Frozen;
        FrozenAtUtc = Guard.Utc(nowUtc);
        return Result.Success();
    }

    /// <summary>Unfreezing extends the expiry date by the frozen duration.</summary>
    public Result Unfreeze(DateTime nowUtc)
    {
        if (Status != PackageStatus.Frozen || FrozenAtUtc is null)
        {
            return Error.Validation("package.unfreeze", "Paket dondurulmuş değil.");
        }

        var frozenFor = Guard.Utc(nowUtc) - FrozenAtUtc.Value;
        if (ExpiresAtUtc is { } exp)
        {
            ExpiresAtUtc = exp + frozenFor;
        }

        Status = PackageStatus.Active;
        FrozenAtUtc = null;
        return Result.Success();
    }

    public Result Extend(int days)
    {
        if (days is < 1 or > 3650)
        {
            return Error.Validation("package.extend", "Uzatma süresi 1 ile 3650 gün arasında olmalıdır.");
        }

        if (ExpiresAtUtc is { } exp)
        {
            ExpiresAtUtc = exp.AddDays(days);
        }

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == PackageStatus.Cancelled)
        {
            return Error.Validation("package.already_cancelled", "Paket zaten iptal edilmiş.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("package.cancel_reason", "İptal nedeni zorunludur.");
        }

        Status = PackageStatus.Cancelled;
        Notes = string.IsNullOrWhiteSpace(Notes) ? $"İptal: {reason.Trim()}" : $"{Notes}\nİptal: {reason.Trim()}";
        return Result.Success();
    }

    public static string StatusName(PackageStatus status) => status switch
    {
        PackageStatus.Active => "Aktif",
        PackageStatus.Frozen => "Dondurulmuş",
        PackageStatus.Cancelled => "İptal",
        _ => status.ToString(),
    };
}
