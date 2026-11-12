using Vernuko.Core;

namespace Vernuko.Appointments;

public enum BookingResourceType
{
    Staff,
    Resource,
}

public enum AppointmentSource
{
    FrontDesk,
    Phone,
    WalkIn,
    WhatsApp,
    Online,
}

/// <summary>
/// One row per occupied staff member / room / device. The [BlockStart, BlockEnd) interval includes preparation and
/// cleanup buffers. A database trigger rejects overlapping active exclusive rows for the same resource, as a last
/// line of defence behind the application-level conflict check.
/// </summary>
public sealed class AppointmentBooking
{
    private AppointmentBooking()
    {
    }

    internal AppointmentBooking(Guid appointmentId, Guid resourceId, BookingResourceType type, UtcRange block, bool isActive)
    {
        Id = Ids.New();
        AppointmentId = appointmentId;
        ResourceId = resourceId;
        ResourceType = type;
        BlockStartUtc = block.Start;
        BlockEndUtc = block.End;
        IsActive = isActive;
        IsExclusive = true;
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid ResourceId { get; private set; }

    public BookingResourceType ResourceType { get; private set; }

    public DateTime BlockStartUtc { get; private set; }

    public DateTime BlockEndUtc { get; private set; }

    public bool IsActive { get; internal set; }

    public bool IsExclusive { get; private set; }

    public UtcRange Block => new(BlockStartUtc, BlockEndUtc);
}

public sealed class AppointmentStatusChange
{
    private AppointmentStatusChange()
    {
    }

    internal AppointmentStatusChange(Guid appointmentId, AppointmentStatus? from, AppointmentStatus to, DateTime atUtc, Guid? userId, string? reason)
    {
        Id = Ids.New();
        AppointmentId = appointmentId;
        FromStatus = from;
        ToStatus = to;
        AtUtc = atUtc;
        UserId = userId;
        Reason = reason;
    }

    public Guid Id { get; private set; }

    public Guid AppointmentId { get; private set; }

    public AppointmentStatus? FromStatus { get; private set; }

    public AppointmentStatus ToStatus { get; private set; }

    public DateTime AtUtc { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Reason { get; private set; }
}

/// <summary>Everything needed to place an appointment on the calendar.</summary>
public sealed record AppointmentSlot(
    DateTime StartUtc,
    int DurationMinutes,
    int PrepMinutes,
    int CleanupMinutes,
    Guid StaffMemberId,
    IReadOnlyList<Guid> ResourceIds)
{
    public DateTime EndUtc => StartUtc.AddMinutes(DurationMinutes);

    public UtcRange ServiceRange => new(StartUtc, EndUtc);

    public UtcRange BlockRange => new(StartUtc.AddMinutes(-PrepMinutes), EndUtc.AddMinutes(CleanupMinutes));

    public IEnumerable<(Guid Id, BookingResourceType Type)> AllResources()
    {
        yield return (StaffMemberId, BookingResourceType.Staff);
        foreach (var id in ResourceIds.Distinct())
        {
            yield return (id, BookingResourceType.Resource);
        }
    }
}

public sealed class Appointment : Entity
{
    public const int MaxNotesLength = 2000;

    private readonly List<AppointmentBooking> _bookings = [];
    private readonly List<AppointmentStatusChange> _history = [];

    private Appointment()
    {
    }

    private Appointment(Guid branchId, Guid customerId, Guid serviceId, AppointmentSlot slot, long priceMinor, AppointmentStatus initial)
    {
        BranchId = branchId;
        CustomerId = customerId;
        ServiceId = serviceId;
        PriceMinor = priceMinor;
        Status = initial;
        ApplySlot(slot);
    }

    public Guid BranchId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ServiceId { get; private set; }

    public Guid StaffMemberId { get; private set; }

    public DateTime StartUtc { get; private set; }

    public DateTime EndUtc { get; private set; }

    public int PrepMinutes { get; private set; }

    public int CleanupMinutes { get; private set; }

    public AppointmentStatus Status { get; private set; }

    /// <summary>Price snapshot at booking time (service price may change later).</summary>
    public long PriceMinor { get; private set; }

    /// <summary>Package whose session this appointment uses (consumed only when completed).</summary>
    public Guid? CustomerPackageId { get; private set; }

    /// <summary>Body area / region (lazer: "Bacak", "Koltuk altı" ...).</summary>
    public string? BodyArea { get; private set; }

    public string? Notes { get; private set; }

    public AppointmentSource Source { get; private set; }

    public string? CancellationReason { get; private set; }

    public int RescheduleCount { get; private set; }

    public DateTime? ArrivedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public IReadOnlyCollection<AppointmentBooking> Bookings => _bookings;

    public IReadOnlyCollection<AppointmentStatusChange> History => _history;

    public UtcRange ServiceRange => new(StartUtc, EndUtc);

    public static Result<Appointment> Create(
        Guid branchId,
        Guid customerId,
        Guid serviceId,
        AppointmentSlot slot,
        long priceMinor,
        AppointmentStatus initialStatus,
        AppointmentSource source,
        string? notes,
        string? bodyArea,
        Guid? customerPackageId,
        Guid? userId,
        DateTime nowUtc)
    {
        if (initialStatus is not (AppointmentStatus.Created or AppointmentStatus.Confirmed or AppointmentStatus.PendingConfirmation))
        {
            return Error.Validation("appointment.initial_status", "Yeni randevu yalnızca Oluşturuldu, Onay bekliyor veya Onaylandı durumunda açılabilir.");
        }

        var check = ValidateSlot(slot);
        if (check.IsFailure)
        {
            return check.Error;
        }

        if (notes is { Length: > MaxNotesLength })
        {
            return Error.Validation("appointment.notes", $"Not en fazla {MaxNotesLength} karakter olabilir.");
        }

        var appointment = new Appointment(branchId, customerId, serviceId, slot, priceMinor, initialStatus)
        {
            Source = source,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            BodyArea = string.IsNullOrWhiteSpace(bodyArea) ? null : bodyArea.Trim(),
            CustomerPackageId = customerPackageId,
            CreatedByUserId = userId,
        };
        appointment.RebuildBookings(slot);
        appointment._history.Add(new AppointmentStatusChange(appointment.Id, null, initialStatus, Guard.Utc(nowUtc), userId, null));
        return appointment;
    }

    public AppointmentSlot CurrentSlot() =>
        new(StartUtc, (int)(EndUtc - StartUtc).TotalMinutes, PrepMinutes, CleanupMinutes, StaffMemberId,
            _bookings.Where(b => b.ResourceType == BookingResourceType.Resource).Select(b => b.ResourceId).ToList());

    /// <summary>
    /// Validates and applies a status transition. The caller must re-run conflict detection when the new status
    /// starts holding resources again (<see cref="WillActivateBookings"/>), and apply package/payment side effects.
    /// </summary>
    public Result ChangeStatus(AppointmentStatus to, DateTime nowUtc, Guid? userId, string? reason)
    {
        Guard.Utc(nowUtc);
        if (to == Status)
        {
            return Result.Success();
        }

        if (!AppointmentStatusRules.CanTransition(Status, to))
        {
            return Error.Validation(
                "appointment.transition",
                $"\"{AppointmentStatusRules.DisplayName(Status)}\" durumundaki randevu \"{AppointmentStatusRules.DisplayName(to)}\" yapılamaz.");
        }

        if (AppointmentStatusRules.RequiresStarted(to) && Status != AppointmentStatus.Completed && nowUtc < StartUtc)
        {
            return Error.Validation("appointment.not_started", "Randevu saati gelmeden bu durum seçilemez.");
        }

        if (AppointmentStatusRules.RequiresReason(Status, to) && string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("appointment.reason", "Bu işlem için bir açıklama girilmelidir.");
        }

        var from = Status;
        Status = to;
        switch (to)
        {
            case AppointmentStatus.Arrived:
                ArrivedAtUtc = nowUtc;
                break;
            case AppointmentStatus.InProgress when from != AppointmentStatus.Completed:
                StartedAtUtc = nowUtc;
                ArrivedAtUtc ??= nowUtc;
                break;
            case AppointmentStatus.InProgress:
                CompletedAtUtc = null;
                break;
            case AppointmentStatus.Completed:
                CompletedAtUtc = nowUtc;
                break;
            case AppointmentStatus.Cancelled:
                CancellationReason = reason?.Trim();
                break;
            case AppointmentStatus.Created when from == AppointmentStatus.Cancelled:
                CancellationReason = null;
                break;
        }

        var holds = AppointmentStatusRules.HoldsResources(to);
        foreach (var booking in _bookings)
        {
            booking.IsActive = holds;
        }

        _history.Add(new AppointmentStatusChange(Id, from, to, nowUtc, userId, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        return Result.Success();
    }

    public static bool WillActivateBookings(AppointmentStatus from, AppointmentStatus to) =>
        !AppointmentStatusRules.HoldsResources(from) && AppointmentStatusRules.HoldsResources(to);

    /// <summary>Moves the appointment to a new slot (time, staff or resources). Conflicts are checked by the caller.</summary>
    public Result Reschedule(AppointmentSlot slot, DateTime nowUtc, Guid? userId)
    {
        if (!AppointmentStatusRules.CanReschedule(Status))
        {
            return Error.Validation("appointment.reschedule_status", $"\"{AppointmentStatusRules.DisplayName(Status)}\" durumundaki randevu yeniden planlanamaz.");
        }

        var check = ValidateSlot(slot);
        if (check.IsFailure)
        {
            return check;
        }

        ApplySlot(slot);
        RescheduleCount++;
        if (Status == AppointmentStatus.Postponed)
        {
            var from = Status;
            Status = AppointmentStatus.Confirmed;
            _history.Add(new AppointmentStatusChange(Id, from, Status, Guard.Utc(nowUtc), userId, "Yeni tarih verildi"));
        }

        RebuildBookings(slot);
        return Result.Success();
    }

    public void UpdateNotes(string? notes) => Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public void LinkPackage(Guid? customerPackageId) => CustomerPackageId = customerPackageId;

    private static Result ValidateSlot(AppointmentSlot slot)
    {
        if (slot.StartUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("appointment.utc", "Randevu saati UTC olmalıdır.");
        }

        if (slot.DurationMinutes is < 5 or > 24 * 60)
        {
            return Error.Validation("appointment.duration", "Randevu süresi 5 dakika ile 24 saat arasında olmalıdır.");
        }

        if (slot.StartUtc.Second != 0 || slot.StartUtc.Millisecond != 0)
        {
            return Error.Validation("appointment.precision", "Randevu saati dakika hassasiyetinde olmalıdır.");
        }

        if (slot.StaffMemberId == Guid.Empty)
        {
            return Error.Validation("appointment.staff", "Personel seçilmelidir.");
        }

        return Result.Success();
    }

    private void ApplySlot(AppointmentSlot slot)
    {
        StartUtc = slot.StartUtc;
        EndUtc = slot.EndUtc;
        PrepMinutes = slot.PrepMinutes;
        CleanupMinutes = slot.CleanupMinutes;
        StaffMemberId = slot.StaffMemberId;
    }

    private void RebuildBookings(AppointmentSlot slot)
    {
        _bookings.Clear();
        var holds = AppointmentStatusRules.HoldsResources(Status);
        foreach (var (id, type) in slot.AllResources())
        {
            _bookings.Add(new AppointmentBooking(Id, id, type, slot.BlockRange, holds));
        }
    }
}
