using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Security;

namespace Vernuko.Application;

// ---------- Setup ----------
public sealed record SetupStaff(string DisplayName, string? Title);

public sealed record SetupServiceItem(string Name, string? Category, int DurationMinutes, long PriceMinor, int PrepMinutes, int CleanupMinutes, ResourceKind? RequiredResourceKind, IReadOnlyList<string>? ResourceKeywords = null, int ProcessingStartMinutes = 0, int ProcessingMinutes = 0);

public sealed record SetupResource(string Name, ResourceKind Kind);

public sealed record SetupRequest(
    BusinessProfileId ProfileId,
    string BusinessName,
    string? Phone,
    string? Address,
    string TimeZoneId,
    WeeklySchedule BusinessHours,
    IReadOnlyList<SetupStaff> Staff,
    IReadOnlyList<SetupServiceItem> Services,
    IReadOnlyList<SetupResource> Resources,
    string AdminDisplayName,
    string AdminUsername,
    string AdminPassword,
    bool EnableLocalBackup,
    string? BackupFolder,
    bool IsDemo = false);

public sealed record SetupResult(Guid AdminUserId, string? RecoveryKey);

// ---------- Users ----------
public sealed record UserSummary(Guid Id, string Username, string DisplayName, SystemRole Role, bool IsActive, DateTime? LastLoginAtUtc)
{
    public string RoleName => RoleDefaults.DisplayName(Role);
}

// ---------- Customers ----------
public sealed record CustomerListItem(Guid Id, int Number, string FullName, string? Phone, string? Email, string State, DateTime? LastVisitUtc);

public sealed record CustomerDetailsDto(
    Guid Id,
    int Number,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    DateOnly? BirthDate,
    string? Address,
    string? Notes,
    string? NationalIdMasked,
    bool AllowSms,
    bool AllowWhatsApp,
    bool AllowEmail,
    bool MarketingConsent,
    string State,
    long BalanceMinor,
    long Version,
    string? CompanyName = null,
    string? TaxOffice = null,
    string? TaxNumber = null)
{
    public string FullName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";

    /// <summary>"Müşteri #8 · 0505 221 33 44 · e-posta" — empty parts left out; the phone never breaks across lines.</summary>
    public string ContactLine => string.Join("  ·  ", new[] { $"Müşteri #{Number}", Phone?.Replace(' ', '\u00A0'), Email }.Where(x => !string.IsNullOrEmpty(x)));

    /// <summary>"Unvan · Vergi dairesi · VKN" for the customer card, null without company data.</summary>
    public string? CompanyLine => CompanyName is null && TaxNumber is null
        ? null
        : string.Join(" · ", new[] { CompanyName, TaxOffice is null ? null : TaxOffice + " V.D.", TaxNumber }.Where(x => !string.IsNullOrEmpty(x)));
}

// ---------- Catalog ----------
public sealed record ServiceDto(Guid Id, string Name, string? Category, int DurationMinutes, int PrepMinutes, int CleanupMinutes, long PriceMinor, ResourceKind? RequiredResourceKind, bool IsActive, IReadOnlyList<Guid>? AllowedResourceIds = null, int ProcessingStartMinutes = 0, int ProcessingMinutes = 0)
{
    public string DisplayPrice => new Money(PriceMinor).ToDisplayString();
}

public sealed record StaffDto(Guid Id, string DisplayName, string? Title, string? Phone, string? Color, bool IsActive, WeeklySchedule? WorkingHours, IReadOnlyList<Guid> ServiceIds);

public sealed record ResourceDto(Guid Id, string Name, ResourceKind Kind, bool IsActive, string? Notes)
{
    public string KindName => BusinessProfileCatalog.ResourceKindName(Kind);
}

public sealed record ScheduleExceptionDto(Guid Id, ScheduleExceptionScope Scope, Guid? TargetId, string? TargetName, ScheduleExceptionKind Kind, DateTime StartLocal, DateTime EndLocal, string? Note);

public sealed record PackageTemplateDto(Guid Id, string Name, Guid ServiceId, string ServiceName, int SessionCount, long PriceMinor, int? ValidityDays, bool IsActive);

// ---------- Appointments ----------
public sealed record AppointmentFilter(Guid? StaffMemberId = null, Guid? ResourceId = null, Guid? ServiceId = null, bool IncludeCancelled = true);

public sealed record AppointmentListItem(
    Guid Id,
    DateTime StartLocal,
    DateTime EndLocal,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    Guid ServiceId,
    string ServiceName,
    string? SessionLabel,
    string? BodyArea,
    Guid StaffMemberId,
    string StaffName,
    string? ResourceNames,
    AppointmentStatus Status,
    string? Notes,
    Guid? CustomerPackageId,
    long PriceMinor,
    Guid? SeriesId = null,
    string? MeetingUrl = null,
    DateTime? StaffFreeFromLocal = null,
    DateTime? StaffFreeUntilLocal = null)
{
    public string StatusName => AppointmentStatusRules.DisplayName(Status);

    /// <summary>"Bekleme 10:30–11:10 (personel boşta)" for services with a waiting part.</summary>
    public string? ProcessingText => StaffFreeFromLocal is { } from && StaffFreeUntilLocal is { } until
        ? $"Bekleme {from:HH:mm}–{until:HH:mm} (personel boşta)"
        : null;

    public bool IsInSeries => SeriesId is not null;

    public int DurationMinutes => (int)(EndLocal - StartLocal).TotalMinutes;

    public IReadOnlyList<AppointmentStatus> NextStatuses => AppointmentStatusRules.NextStatuses(Status);

    public bool CanReschedule => AppointmentStatusRules.CanReschedule(Status);
}

public sealed record DayAgenda(DateOnly Day, IReadOnlyList<AppointmentListItem> Items, Reporting.DailySummary Summary, int ActiveStaffCount, IReadOnlyList<TimeInterval> Breaks, bool IsBusinessOpen);

public sealed record CreateAppointmentRequest(
    Guid CustomerId,
    Guid ServiceId,
    Guid StaffMemberId,
    IReadOnlyList<Guid> ResourceIds,
    DateTime StartLocal,
    int? DurationMinutesOverride,
    string? Notes,
    string? BodyArea,
    Guid? CustomerPackageId,
    AppointmentSource Source = AppointmentSource.FrontDesk,
    string? MeetingUrl = null);

/// <param name="ApplyToFollowing">For recurring series: move all later open appointments of the series by the same local-time shift (and staff/rooms).</param>
public sealed record RescheduleRequest(Guid AppointmentId, DateTime NewStartLocal, Guid? StaffMemberId, IReadOnlyList<Guid>? ResourceIds, int? DurationMinutes = null, bool ApplyToFollowing = false);

/// <summary>Result of creating a recurring series: which dates were booked, which were skipped and why.</summary>
public sealed record SeriesOutcome(Guid? SeriesId, IReadOnlyList<DateTime> CreatedLocal, IReadOnlyList<(DateTime StartLocal, IReadOnlyList<SchedulingIssue> Issues)> Problems)
{
    public bool Saved => SeriesId is not null;

    public bool NeedsConfirmation => !Saved && Problems.Count > 0;
}

public sealed record WaitlistItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    Guid? ServiceId,
    string? ServiceName,
    Guid? StaffMemberId,
    string? StaffName,
    DateOnly FromDate,
    DateOnly ToDate,
    string? PreferredTime,
    string? Notes,
    WaitlistStatus Status,
    DateTime CreatedAtUtc)
{
    public string StatusName => WaitlistEntry.StatusName(Status);

    public string RangeText => FromDate == ToDate ? FromDate.ToString("dd.MM.yyyy", Core.TurkishText.Culture) : $"{FromDate.ToString("dd.MM", Core.TurkishText.Culture)} – {ToDate.ToString("dd.MM.yyyy", Core.TurkishText.Culture)}";
}

/// <summary>Outcome of a booking attempt: saved, or the issues that block it / need explicit confirmation.</summary>
public sealed record BookingOutcome(Guid? AppointmentId, IReadOnlyList<SchedulingIssue> Issues)
{
    public bool Saved => AppointmentId is not null;

    public bool NeedsConfirmation => !Saved && Issues.Count > 0 && Issues.All(i => i.Severity == IssueSeverity.RequiresConfirmation);
}

/// <param name="SearchAllowedResources">
/// When the service is limited to specific rooms/devices, also look in the other allowed ones (e.g. "Lazer Odası 2"
/// when room 1 is full). The selected resources are tried first.
/// </param>
public sealed record SlotQuery(DateOnly Day, Guid ServiceId, Guid StaffMemberId, IReadOnlyList<Guid> ResourceIds, int? DurationMinutesOverride = null, Guid? IgnoreAppointmentId = null, bool SearchAllowedResources = false);

/// <summary>A free start time and the rooms/devices it is free in.</summary>
public sealed record SlotSuggestion(TimeOnly LocalStart, DateTime StartUtc, IReadOnlyList<Guid> ResourceIds);

// ---------- Packages ----------
public sealed record SellPackageRequest(
    Guid CustomerId,
    Guid ServiceId,
    Guid? TemplateId,
    string Name,
    string? BodyArea,
    int Sessions,
    long PriceMinor,
    int? ValidityDays,
    long InitialPaymentMinor,
    PaymentMethod? PaymentMethod,
    string IdempotencyKey,
    Guid? SoldByStaffId = null);

public sealed record CustomerPackageDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid ServiceId,
    string ServiceName,
    string Name,
    string? BodyArea,
    int TotalSessions,
    int UsedSessions,
    int RemainingSessions,
    int UpcomingBooked,
    DateTime PurchasedAtUtc,
    DateTime? ExpiresAtUtc,
    PackageStatus Status,
    long PriceMinor)
{
    public string StatusName => CustomerPackage.StatusName(Status);

    public string Label => BodyArea is null ? Name : $"{Name} · {BodyArea}";
}

public sealed record SessionLedgerDto(DateTime AtUtc, SessionEntryKind Kind, int Delta, Guid? AppointmentId, string? Reason, string? UserName)
{
    public string KindName => Kind switch
    {
        SessionEntryKind.Consumed => "Seans kullanıldı",
        SessionEntryKind.Restored => "Seans iade edildi",
        _ => "Düzeltme",
    };
}

// ---------- Payments ----------
public sealed record RecordTransactionRequest(
    Guid CustomerId,
    TransactionKind Kind,
    long AmountMinor,
    PaymentMethod? Method,
    bool IsDeposit,
    Guid? AppointmentId,
    Guid? CustomerPackageId,
    string? Description,
    string IdempotencyKey);

public sealed record TransactionDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    TransactionKind Kind,
    long AmountMinor,
    long BalanceEffectMinor,
    PaymentMethod? Method,
    bool IsDeposit,
    string? Description,
    DateTime OccurredAtUtc,
    Guid? ReversesTransactionId,
    bool IsReversed)
{
    public string KindName => CustomerTransaction.KindName(Kind);

    public string MethodName => CustomerTransaction.MethodName(Method);
}

public sealed record CashDaySummary(DateOnly Day, IReadOnlyDictionary<PaymentMethod, long> NetByMethod, long TotalNetMinor, IReadOnlyList<TransactionDto> Transactions);

public sealed record DebtorDto(Guid CustomerId, string CustomerName, string? Phone, long BalanceMinor);

// ---------- Reports ----------
public sealed record PeriodReport(
    DateOnly From,
    DateOnly To,
    int TotalAppointments,
    int Completed,
    int Cancelled,
    int NoShow,
    double CancellationRate,
    double NoShowRate,
    IReadOnlyList<(string Name, int Count)> ByStaff,
    IReadOnlyList<(string Name, int Count)> ByService,
    int PackagesSold,
    int SessionsConsumed,
    long? CollectedMinor,
    long? OutstandingMinor);
