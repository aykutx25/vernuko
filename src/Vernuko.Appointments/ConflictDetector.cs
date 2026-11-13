using Vernuko.Core;

namespace Vernuko.Appointments;

public sealed record ExistingBooking(Guid AppointmentId, Guid ResourceId, UtcRange Block);

public sealed record BookingConflict(Guid ResourceId, BookingResourceType ResourceType, Guid ConflictingAppointmentId, UtcRange ConflictingBlock);

/// <summary>Pure overlap detection for staff, rooms and devices (half-open intervals; back-to-back is allowed).</summary>
public static class ConflictDetector
{
    public static IReadOnlyList<BookingConflict> Find(AppointmentSlot requested, IEnumerable<ExistingBooking> existing, Guid? ignoreAppointmentId = null)
    {
        // Each requested row against existing rows of the same staff member/room/device (a waiting part leaves the
        // staff member's middle free, so another appointment may sit exactly there).
        var blocks = requested.Blocks().ToList();
        return existing
            .Where(e => e.AppointmentId != ignoreAppointmentId)
            .SelectMany(e => blocks.Where(b => b.Id == e.ResourceId && e.Block.Overlaps(b.Block)).Take(1)
                .Select(b => new BookingConflict(e.ResourceId, b.Type, e.AppointmentId, e.Block)))
            .ToList();
    }
}
