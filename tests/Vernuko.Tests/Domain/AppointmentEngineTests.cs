using Vernuko.Appointments;
using Vernuko.Core;

namespace Vernuko.Tests.Domain;

public class AppointmentEngineTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZones.Find(TimeZones.Turkey);
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Room = Guid.NewGuid();
    private static readonly DateOnly Thursday = new(2026, 11, 12);

    private static DateTime Utc(int hour, int minute = 0) => TimeZones.ToUtc(Thursday.ToDateTime(new TimeOnly(hour, minute)), Istanbul);

    private static AppointmentSlot Slot(int hour, int minute = 0, int duration = 30, int prep = 0, int cleanup = 0, Guid? staff = null, params Guid[] resources) =>
        new(Utc(hour, minute), duration, prep, cleanup, staff ?? Staff, resources);

    private static DayContext Context(IReadOnlyList<ExistingBooking>? bookings = null, IReadOnlyList<ScheduleException>? exceptions = null, WeeklySchedule? staffHours = null) =>
        new(Thursday, Istanbul, WeeklySchedule.DefaultBusinessWeek(), staffHours, exceptions ?? [], bookings ?? []);

    [Fact]
    public void Status_transitions_follow_rules()
    {
        Assert.True(AppointmentStatusRules.CanTransition(AppointmentStatus.Confirmed, AppointmentStatus.Arrived));
        Assert.True(AppointmentStatusRules.CanTransition(AppointmentStatus.InProgress, AppointmentStatus.Completed));
        Assert.False(AppointmentStatusRules.CanTransition(AppointmentStatus.Cancelled, AppointmentStatus.Completed));
        Assert.False(AppointmentStatusRules.CanTransition(AppointmentStatus.Completed, AppointmentStatus.Cancelled));
        Assert.False(AppointmentStatusRules.HoldsResources(AppointmentStatus.Cancelled));
        Assert.True(AppointmentStatusRules.HoldsResources(AppointmentStatus.Completed));
        Assert.Equal(10, Enum.GetValues<AppointmentStatus>().Length);
        foreach (var s in Enum.GetValues<AppointmentStatus>())
        {
            Assert.False(string.IsNullOrWhiteSpace(AppointmentStatusRules.DisplayName(s)));
        }
    }

    [Fact]
    public void Cannot_complete_before_start_and_cancel_needs_reason()
    {
        var appt = Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Slot(15), 0, AppointmentStatus.Created, AppointmentSource.FrontDesk, null, null, null, null, Utc(9)).Value;
        Assert.True(appt.ChangeStatus(AppointmentStatus.Completed, Utc(10), null, null).IsFailure);
        Assert.True(appt.ChangeStatus(AppointmentStatus.Cancelled, Utc(10), null, " ").IsFailure);
        Assert.True(appt.ChangeStatus(AppointmentStatus.Cancelled, Utc(10), null, "Müşteri aradı").IsSuccess);
        Assert.All(appt.Bookings, b => Assert.False(b.IsActive));
        Assert.Equal(2, appt.History.Count);
    }

    [Fact]
    public void Back_to_back_bookings_do_not_conflict_but_overlaps_do()
    {
        var existing = new[] { new ExistingBooking(Guid.NewGuid(), Staff, new UtcRange(Utc(10), Utc(10, 30))) };
        Assert.Empty(ConflictDetector.Find(Slot(10, 30), existing));
        Assert.Single(ConflictDetector.Find(Slot(10, 15), existing));
        Assert.Empty(ConflictDetector.Find(Slot(10, 15, staff: Guid.NewGuid()), existing));
    }

    [Fact]
    public void Cleanup_buffer_blocks_the_room()
    {
        var other = Guid.NewGuid();
        var existing = new[] { new ExistingBooking(other, Room, Slot(10, 0, 30, 0, 15, Guid.NewGuid(), Room).BlockRange) };
        var issues = AvailabilityCalculator.Validate(Context(existing), Slot(10, 30, resources: Room));
        Assert.Contains(issues, i => i.Code == "conflict.resource" && i.Severity == IssueSeverity.Blocking);
        Assert.Empty(AvailabilityCalculator.Validate(Context(existing), Slot(10, 45, resources: Room)));
    }

    [Fact]
    public void Lunch_break_and_closing_time_require_confirmation()
    {
        var issues = AvailabilityCalculator.Validate(Context(), Slot(12, 45));
        Assert.Contains(issues, i => i.Code == "hours.business" && i.Severity == IssueSeverity.RequiresConfirmation);
        Assert.NotEmpty(AvailabilityCalculator.Validate(Context(), Slot(18, 45)));
        Assert.Empty(AvailabilityCalculator.Validate(Context(), Slot(18, 30)));
    }

    [Fact]
    public void Staff_leave_blocks_booking()
    {
        var leave = new ScheduleException(ScheduleExceptionScope.Staff, Staff, ScheduleExceptionKind.Leave, Utc(0), Utc(23, 59), "Yıllık izin");
        var issues = AvailabilityCalculator.Validate(Context(exceptions: [leave]), Slot(11));
        Assert.Contains(issues, i => i.Code == "staff.leave" && i.Severity == IssueSeverity.Blocking);
    }

    [Fact]
    public void Free_slots_skip_breaks_bookings_and_past_times()
    {
        var existing = new[] { new ExistingBooking(Guid.NewGuid(), Staff, new UtcRange(Utc(14), Utc(15))) };
        var slots = AvailabilityCalculator.FindSlots(Context(existing), new AppointmentSlotTemplate(30, 0, 0, Staff, []), 30, Utc(10));
        var times = slots.Select(s => s.LocalStart).ToList();
        Assert.DoesNotContain(new TimeOnly(9, 30), times);      // past
        Assert.Contains(new TimeOnly(10, 0), times);
        Assert.Contains(new TimeOnly(12, 0), times);
        Assert.DoesNotContain(new TimeOnly(12, 30), times);     // lunch break
        Assert.Contains(new TimeOnly(13, 30), times);
        Assert.DoesNotContain(new TimeOnly(14, 0), times);      // booked
        Assert.DoesNotContain(new TimeOnly(14, 30), times);     // booked
        Assert.Contains(new TimeOnly(15, 0), times);
        Assert.Equal(new TimeOnly(18, 30), times[^1]);          // last 30-min slot before 19:00
    }

    [Fact]
    public void Staff_hours_narrow_business_hours()
    {
        var staffHours = WeeklySchedule.Create([DayOfWeek.Thursday], new TimeOnly(13, 30), new TimeOnly(17, 0));
        var slots = AvailabilityCalculator.FindSlots(Context(staffHours: staffHours), new AppointmentSlotTemplate(60, 0, 0, Staff, []), 60, Utc(0));
        Assert.Equal([new TimeOnly(13, 30), new TimeOnly(14, 30), new TimeOnly(15, 30)], slots.Select(s => s.LocalStart));
    }

    [Fact]
    public void Daylight_saving_day_has_23_hours_and_slots_stay_on_local_clock()
    {
        var berlin = TimeZones.Find("Europe/Berlin");
        var springForward = new DateOnly(2026, 3, 29);
        var range = TimeZones.DayRange(springForward, berlin);
        Assert.Equal(TimeSpan.FromHours(23), range.Duration);
        Assert.Equal(TimeSpan.FromHours(25), TimeZones.DayRange(new DateOnly(2026, 10, 25), berlin).Duration);

        var hours = WeeklySchedule.Create([DayOfWeek.Sunday], new TimeOnly(1, 0), new TimeOnly(5, 0));
        var ctx = new DayContext(springForward, berlin, hours, null, [], []);
        var slots = AvailabilityCalculator.FindSlots(ctx, new AppointmentSlotTemplate(60, 0, 0, Staff, []), 60, DateTime.MinValue.ToUniversalTime());
        // 02:00 local does not exist on this day.
        Assert.DoesNotContain(new TimeOnly(2, 0), slots.Select(s => s.LocalStart));
        Assert.Contains(new TimeOnly(3, 0), slots.Select(s => s.LocalStart));
        Assert.All(slots, s => Assert.Equal(DateTimeKind.Utc, s.StartUtc.Kind));
    }

    [Fact]
    public void Istanbul_has_no_DST_and_local_conversion_is_exact()
    {
        var utc = TimeZones.ToUtc(new DateTime(2026, 7, 1, 9, 0, 0), Istanbul);
        Assert.Equal(new DateTime(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(TimeSpan.FromHours(24), TimeZones.DayRange(new DateOnly(2026, 3, 29), Istanbul).Duration);
    }
}
