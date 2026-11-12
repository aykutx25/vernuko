using Vernuko.Core;

namespace Vernuko.Appointments;

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>
/// "Every N days/weeks/months, Count times". Occurrences keep the same LOCAL wall-clock time, so a weekly 10:00
/// session stays at 10:00 across daylight-saving changes. Monthly on the 31st falls back to the month's last day.
/// </summary>
public sealed record RecurrencePattern(RecurrenceFrequency Frequency, int Interval, int Count)
{
    public const int MaxCount = 52;

    public Result Validate()
    {
        if (Interval is < 1 or > 12)
        {
            return Error.Validation("recurrence.interval", "Tekrar aralığı 1 ile 12 arasında olmalıdır.");
        }

        return Count is < 2 or > MaxCount
            ? Error.Validation("recurrence.count", $"Tekrar sayısı 2 ile {MaxCount} arasında olmalıdır.")
            : Result.Success();
    }

    public IReadOnlyList<DateTime> Occurrences(DateTime firstLocal)
    {
        var list = new List<DateTime>(Count);
        for (var i = 0; i < Count; i++)
        {
            list.Add(Frequency switch
            {
                RecurrenceFrequency.Daily => firstLocal.AddDays(i * Interval),
                RecurrenceFrequency.Weekly => firstLocal.AddDays(7 * i * Interval),
                _ => firstLocal.AddMonths(i * Interval),
            });
        }

        return list;
    }

    public string Describe() => Frequency switch
    {
        RecurrenceFrequency.Daily => Interval == 1 ? $"Her gün, {Count} kez" : $"{Interval} günde bir, {Count} kez",
        RecurrenceFrequency.Weekly => Interval == 1 ? $"Her hafta, {Count} kez" : $"{Interval} haftada bir, {Count} kez",
        _ => Interval == 1 ? $"Her ay, {Count} kez" : $"{Interval} ayda bir, {Count} kez",
    };
}

public enum WaitlistStatus
{
    Waiting,
    Contacted,
    Booked,
    Cancelled,
}

/// <summary>A customer waiting for a free slot (bekleme listesi).</summary>
public sealed class WaitlistEntry : Entity
{
    private WaitlistEntry()
    {
    }

    private WaitlistEntry(Guid customerId, Guid? serviceId, Guid? staffMemberId, DateOnly fromDate, DateOnly toDate)
    {
        CustomerId = customerId;
        ServiceId = serviceId;
        StaffMemberId = staffMemberId;
        FromDate = fromDate;
        ToDate = toDate;
        Status = WaitlistStatus.Waiting;
    }

    public Guid CustomerId { get; private set; }

    public Guid? ServiceId { get; private set; }

    public Guid? StaffMemberId { get; private set; }

    public DateOnly FromDate { get; private set; }

    public DateOnly ToDate { get; private set; }

    /// <summary>Free text such as "hafta içi öğleden sonra".</summary>
    public string? PreferredTime { get; private set; }

    public string? Notes { get; private set; }

    public WaitlistStatus Status { get; private set; }

    public Guid? BookedAppointmentId { get; private set; }

    public static Result<WaitlistEntry> Create(Guid customerId, Guid? serviceId, Guid? staffMemberId, DateOnly fromDate, DateOnly toDate, string? preferredTime, string? notes)
    {
        if (toDate < fromDate)
        {
            return Error.Validation("waitlist.range", "Bitiş tarihi başlangıçtan önce olamaz.");
        }

        if (toDate.DayNumber - fromDate.DayNumber > 366)
        {
            return Error.Validation("waitlist.range_long", "Bekleme aralığı en fazla bir yıl olabilir.");
        }

        return new WaitlistEntry(customerId, serviceId, staffMemberId, fromDate, toDate)
        {
            PreferredTime = string.IsNullOrWhiteSpace(preferredTime) ? null : preferredTime.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
    }

    public bool IsOpen => Status is WaitlistStatus.Waiting or WaitlistStatus.Contacted;

    /// <summary>Whether a freed slot (service, staff, local day) suits this entry.</summary>
    public bool Matches(Guid serviceId, Guid staffMemberId, DateOnly day) =>
        IsOpen && FromDate <= day && day <= ToDate
        && (ServiceId is null || ServiceId == serviceId)
        && (StaffMemberId is null || StaffMemberId == staffMemberId);

    public Result MarkContacted()
    {
        if (!IsOpen)
        {
            return Error.Validation("waitlist.closed", "Bu kayıt kapanmış.");
        }

        Status = WaitlistStatus.Contacted;
        return Result.Success();
    }

    public Result MarkBooked(Guid appointmentId)
    {
        if (!IsOpen)
        {
            return Error.Validation("waitlist.closed", "Bu kayıt kapanmış.");
        }

        Status = WaitlistStatus.Booked;
        BookedAppointmentId = appointmentId;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (!IsOpen)
        {
            return Error.Validation("waitlist.closed", "Bu kayıt kapanmış.");
        }

        Status = WaitlistStatus.Cancelled;
        return Result.Success();
    }

    public static string StatusName(WaitlistStatus status) => status switch
    {
        WaitlistStatus.Waiting => "Bekliyor",
        WaitlistStatus.Contacted => "Arandı",
        WaitlistStatus.Booked => "Randevu verildi",
        _ => "İptal",
    };
}
