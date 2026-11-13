using Vernuko.Core;

namespace Vernuko.Appointments;

/// <summary>A service offered by the business (hizmet).</summary>
public sealed class ServiceDefinition : Entity
{
    private ServiceDefinition()
    {
    }

    public ServiceDefinition(string name, string? category, int durationMinutes, long priceMinor, int prepMinutes = 0, int cleanupMinutes = 0, ResourceKind? requiredResourceKind = null)
    {
        Name = Guard.NotBlank(name).Trim();
        IsActive = true;
        var r = Update(name, category, durationMinutes, priceMinor, prepMinutes, cleanupMinutes, requiredResourceKind);
        if (r.IsFailure)
        {
            throw new ArgumentException(r.Error.Message);
        }
    }

    public string Name { get; private set; } = string.Empty;

    public string? Category { get; private set; }

    public int DurationMinutes { get; private set; }

    public int PrepMinutes { get; private set; }

    public int CleanupMinutes { get; private set; }

    public long PriceMinor { get; private set; }

    public ResourceKind? RequiredResourceKind { get; private set; }

    /// <summary>
    /// "Bekleme süresi" (e.g. hair dye processing): from this many minutes after the start the staff member is
    /// free for ProcessingMinutes and may serve someone else; the chair/room stays occupied. 0 = none.
    /// </summary>
    public int ProcessingStartMinutes { get; private set; }

    public int ProcessingMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public string? Color { get; private set; }

    /// <summary>The waiting part must lie inside the service and leave at least 5 minutes of work before and after.</summary>
    public static Result ValidateProcessing(int durationMinutes, int startMinutes, int minutes)
    {
        if (minutes == 0 && startMinutes == 0)
        {
            return Result.Success();
        }

        if (minutes < 5 || startMinutes < 5 || startMinutes + minutes > durationMinutes - 5)
        {
            return Error.Validation("service.processing", "Bekleme süresi en az 5 dakika olmalı ve hizmetin başında ve sonunda en az 5 dakikalık iş kalmalı (ör. 90 dk boya: 30. dakikadan sonra 40 dk bekleme).");
        }

        return Result.Success();
    }

    public Result SetProcessing(int startMinutes, int minutes)
    {
        var v = ValidateProcessing(DurationMinutes, startMinutes, minutes);
        if (v.IsFailure)
        {
            return v;
        }

        ProcessingStartMinutes = minutes == 0 ? 0 : startMinutes;
        ProcessingMinutes = minutes;
        return Result.Success();
    }

    public static Result ValidateValues(string? name, int durationMinutes, long priceMinor, int prepMinutes, int cleanupMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("service.name", "Hizmet adı zorunludur.");
        }

        if (durationMinutes is < 5 or > 24 * 60)
        {
            return Error.Validation("service.duration", "Hizmet süresi 5 dakika ile 24 saat arasında olmalıdır.");
        }

        if (prepMinutes is < 0 or > 240 || cleanupMinutes is < 0 or > 240)
        {
            return Error.Validation("service.buffers", "Hazırlık ve temizlik süreleri 0–240 dakika arasında olmalıdır.");
        }

        if (priceMinor < 0)
        {
            return Error.Validation("service.price", "Fiyat negatif olamaz.");
        }

        return Result.Success();
    }

    public Result Update(string name, string? category, int durationMinutes, long priceMinor, int prepMinutes, int cleanupMinutes, ResourceKind? requiredResourceKind)
    {
        var v = ValidateValues(name, durationMinutes, priceMinor, prepMinutes, cleanupMinutes);
        if (v.IsFailure)
        {
            return v;
        }

        Name = name.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        DurationMinutes = durationMinutes;
        PriceMinor = priceMinor;
        PrepMinutes = prepMinutes;
        CleanupMinutes = cleanupMinutes;
        RequiredResourceKind = requiredResourceKind;
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;

    public void SetColor(string? color) => Color = color;
}

/// <summary>A person who delivers services (uzman, doktor, eğitmen...).</summary>
public sealed class StaffMember : Entity
{
    private StaffMember()
    {
    }

    public StaffMember(Guid branchId, string displayName, string? title, WeeklySchedule? workingHours = null)
    {
        BranchId = branchId;
        DisplayName = Guard.NotBlank(displayName).Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        WorkingHours = workingHours;
        IsActive = true;
    }

    public Guid BranchId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public string? Phone { get; private set; }

    public string? Color { get; private set; }

    /// <summary>Personal weekly hours; null means "same as business hours".</summary>
    public WeeklySchedule? WorkingHours { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(string displayName, string? title, string? phone, string? color)
    {
        DisplayName = Guard.NotBlank(displayName).Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Color = color;
    }

    public Result SetWorkingHours(WeeklySchedule? hours)
    {
        if (hours is not null)
        {
            var v = hours.Validate();
            if (v.IsFailure)
            {
                return v;
            }
        }

        WorkingHours = hours;
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;

    public void SetSortOrder(int order) => SortOrder = order;
}

/// <summary>Links staff to the services they can deliver. A service without links can be delivered by anyone.</summary>
public sealed class StaffService
{
    private StaffService()
    {
    }

    public StaffService(Guid staffMemberId, Guid serviceId)
    {
        StaffMemberId = staffMemberId;
        ServiceId = serviceId;
    }

    public Guid StaffMemberId { get; private set; }

    public Guid ServiceId { get; private set; }
}

/// <summary>
/// A room or device a service may use. When a service has links, a booking must include one linked resource of
/// every linked kind (e.g. laser: one of the laser rooms + the laser device) and no unlinked resource of those kinds.
/// Without links the older "requires a room" rule applies.
/// </summary>
public sealed class ServiceResource
{
    private ServiceResource()
    {
    }

    public ServiceResource(Guid serviceId, Guid resourceId)
    {
        ServiceId = serviceId;
        ResourceId = resourceId;
    }

    public Guid ServiceId { get; private set; }

    public Guid ResourceId { get; private set; }
}

/// <summary>A bookable room, device, chair, unit, lift, studio ...</summary>
public sealed class Resource : Entity
{
    private Resource()
    {
    }

    public Resource(Guid branchId, string name, ResourceKind kind, int capacity = 1)
    {
        BranchId = branchId;
        Name = Guard.NotBlank(name).Trim();
        Kind = kind;
        Capacity = Math.Max(1, capacity);
        IsActive = true;
    }

    public Guid BranchId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ResourceKind Kind { get; private set; }

    /// <summary>1 = exclusive (default). Values above 1 are reserved for group classes (later phase).</summary>
    public int Capacity { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public void Update(string name, ResourceKind kind, string? notes)
    {
        Name = Guard.NotBlank(name).Trim();
        Kind = kind;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void SetActive(bool active) => IsActive = active;
}

public enum ScheduleExceptionScope
{
    Business,
    Staff,
    Resource,
}

public enum ScheduleExceptionKind
{
    Leave,
    PublicHoliday,
    Closure,
    Maintenance,
    Other,
}

/// <summary>Leave, public holiday, closure or maintenance window.</summary>
public sealed class ScheduleException : Entity
{
    private ScheduleException()
    {
    }

    public ScheduleException(ScheduleExceptionScope scope, Guid? targetId, ScheduleExceptionKind kind, DateTime startUtc, DateTime endUtc, string? note)
    {
        if (scope != ScheduleExceptionScope.Business && targetId is null)
        {
            throw new ArgumentException("Staff/resource exceptions need a target.", nameof(targetId));
        }

        _ = new UtcRange(startUtc, endUtc);
        Scope = scope;
        TargetId = scope == ScheduleExceptionScope.Business ? null : targetId;
        Kind = kind;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public ScheduleExceptionScope Scope { get; private set; }

    public Guid? TargetId { get; private set; }

    public ScheduleExceptionKind Kind { get; private set; }

    public DateTime StartUtc { get; private set; }

    public DateTime EndUtc { get; private set; }

    public string? Note { get; private set; }

    public UtcRange Range => new(StartUtc, EndUtc);

    public static string KindName(ScheduleExceptionKind kind) => kind switch
    {
        ScheduleExceptionKind.Leave => "İzin",
        ScheduleExceptionKind.PublicHoliday => "Resmî tatil",
        ScheduleExceptionKind.Closure => "Kapalı",
        ScheduleExceptionKind.Maintenance => "Bakım",
        _ => "Diğer",
    };
}
