using Vernuko.Core;

namespace Vernuko.Appointments;

/// <summary>Input for availability and validation: the calendar situation of one local day.</summary>
public sealed record DayContext(
    DateOnly Day,
    TimeZoneInfo Zone,
    WeeklySchedule BusinessHours,
    WeeklySchedule? StaffHours,
    IReadOnlyList<ScheduleException> Exceptions,
    IReadOnlyList<ExistingBooking> Bookings);

public sealed record AvailableSlot(TimeOnly LocalStart, DateTime StartUtc);

/// <summary>
/// Computes free start times and validates a requested slot against working hours, leave, closures and
/// existing bookings. Works in the business' local time and converts to UTC (DST-safe).
/// </summary>
public static class AvailabilityCalculator
{
    public static IReadOnlyList<AvailableSlot> FindSlots(DayContext ctx, AppointmentSlotTemplate template, int stepMinutes, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepMinutes, 5);
        var result = new List<AvailableSlot>();
        var step = TimeSpan.FromMinutes(stepMinutes);
        var duration = TimeSpan.FromMinutes(template.DurationMinutes);
        foreach (var interval in OpenIntervals(ctx))
        {
            var endSpan = EndSpan(interval.End);
            for (var t = AlignUp(interval.Start.ToTimeSpan(), TimeSpan.FromMinutes(5)); t + duration <= endSpan && t < TimeSpan.FromDays(1); t += step)
            {
                var local = TimeOnly.FromTimeSpan(t);
                var startUtc = TimeZones.ToUtc(ctx.Day.ToDateTime(local), ctx.Zone);
                if (startUtc < nowUtc || TimeZones.LocalDate(startUtc, ctx.Zone) != ctx.Day)
                {
                    continue;
                }

                if (Validate(ctx, template.At(startUtc)).Count == 0)
                {
                    result.Add(new AvailableSlot(TimeOnly.FromDateTime(TimeZones.ToLocal(startUtc, ctx.Zone)), startUtc));
                }
            }
        }

        return result;
    }

    /// <summary>All problems with placing <paramref name="slot"/> on this day.</summary>
    public static IReadOnlyList<SchedulingIssue> Validate(DayContext ctx, AppointmentSlot slot, Guid? ignoreAppointmentId = null)
    {
        var issues = new List<SchedulingIssue>();
        var service = slot.ServiceRange;

        var conflicts = ConflictDetector.Find(slot, ctx.Bookings, ignoreAppointmentId);
        if (conflicts.Any(c => c.ResourceType == BookingResourceType.Staff))
        {
            issues.Add(new SchedulingIssue("conflict.staff", "Seçilen personelin bu saatte başka bir randevusu var.", IssueSeverity.Blocking));
        }

        if (conflicts.Any(c => c.ResourceType == BookingResourceType.Resource))
        {
            issues.Add(new SchedulingIssue("conflict.resource", "Seçilen oda/cihaz bu saatte başka bir randevuda kullanılıyor.", IssueSeverity.Blocking));
        }

        foreach (var ex in ctx.Exceptions)
        {
            if (!ex.Range.Overlaps(service))
            {
                continue;
            }

            switch (ex.Scope)
            {
                case ScheduleExceptionScope.Staff when ex.TargetId == slot.StaffMemberId:
                    issues.Add(new SchedulingIssue("staff.leave", $"Personel bu saatte izinli ({ScheduleException.KindName(ex.Kind)}).", IssueSeverity.Blocking));
                    break;
                case ScheduleExceptionScope.Resource when ex.TargetId is { } rid && slot.ResourceIds.Contains(rid):
                    issues.Add(new SchedulingIssue("resource.unavailable", $"Seçilen oda/cihaz bu saatte kullanılamıyor ({ScheduleException.KindName(ex.Kind)}).", IssueSeverity.Blocking));
                    break;
                case ScheduleExceptionScope.Business:
                    issues.Add(new SchedulingIssue("business.closed", $"İşletme bu tarihte kapalı ({ScheduleException.KindName(ex.Kind)}{(ex.Note is null ? string.Empty : ": " + ex.Note)}).", IssueSeverity.RequiresConfirmation));
                    break;
            }
        }

        var localStart = TimeZones.ToLocal(service.Start, ctx.Zone);
        var localEnd = TimeZones.ToLocal(service.End, ctx.Zone);
        if (!FitsSchedule(ctx.BusinessHours, localStart, localEnd))
        {
            issues.Add(new SchedulingIssue("hours.business", "Randevu işletmenin çalışma saatleri dışında.", IssueSeverity.RequiresConfirmation));
        }
        else if (ctx.StaffHours is not null && !FitsSchedule(ctx.StaffHours, localStart, localEnd))
        {
            issues.Add(new SchedulingIssue("hours.staff", "Randevu personelin çalışma saatleri dışında.", IssueSeverity.RequiresConfirmation));
        }

        return issues;
    }

    private static bool FitsSchedule(WeeklySchedule schedule, DateTime localStart, DateTime localEnd)
    {
        if (localStart.Date != localEnd.Date && localEnd.TimeOfDay != TimeSpan.Zero)
        {
            return false;
        }

        var start = TimeOnly.FromDateTime(localStart);
        var endSpan = localEnd.Date > localStart.Date ? TimeSpan.FromDays(1) : localEnd.TimeOfDay;
        return schedule.For(localStart.DayOfWeek).Any(i => i.Start <= start && endSpan <= EndSpan(i.End));
    }

    private static TimeSpan EndSpan(TimeOnly end) => end == TimeOnly.MaxValue ? TimeSpan.FromDays(1) : end.ToTimeSpan();

    private static IEnumerable<TimeInterval> OpenIntervals(DayContext ctx)
    {
        var business = ctx.BusinessHours.For(ctx.Day.DayOfWeek);
        if (ctx.StaffHours is null)
        {
            return business.OrderBy(i => i.Start);
        }

        var staff = ctx.StaffHours.For(ctx.Day.DayOfWeek);
        var result = new List<TimeInterval>();
        foreach (var b in business)
        {
            foreach (var s in staff)
            {
                var start = b.Start > s.Start ? b.Start : s.Start;
                var end = b.End < s.End ? b.End : s.End;
                if (end > start)
                {
                    result.Add(new TimeInterval(start, end));
                }
            }
        }

        return result.OrderBy(i => i.Start);
    }

    /// <summary>Candidates start at the (5-minute aligned) opening time, then advance by the slot step.</summary>
    private static TimeSpan AlignUp(TimeSpan t, TimeSpan step)
    {
        var ticks = (t.Ticks + step.Ticks - 1) / step.Ticks * step.Ticks;
        return TimeSpan.FromTicks(ticks);
    }
}

/// <summary>Service duration/buffers + chosen staff and resources, without a start time yet.</summary>
public sealed record AppointmentSlotTemplate(int DurationMinutes, int PrepMinutes, int CleanupMinutes, Guid StaffMemberId, IReadOnlyList<Guid> ResourceIds)
{
    public AppointmentSlot At(DateTime startUtc) => new(startUtc, DurationMinutes, PrepMinutes, CleanupMinutes, StaffMemberId, ResourceIds);
}
