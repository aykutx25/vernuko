using Vernuko.Core;

namespace Vernuko.Appointments;

public sealed record ExistingBooking(Guid AppointmentId, Guid ResourceId, UtcRange Block);

public sealed record BookingConflict(Guid ResourceId, BookingResourceType ResourceType, Guid ConflictingAppointmentId, UtcRange ConflictingBlock);

/// <summary>Pure overlap detection for staff, rooms and devices (half-open intervals; back-to-back is allowed).</summary>
public static class ConflictDetector
{
    public static IReadOnlyList<BookingConflict> Find(AppointmentSlot requested, IEnumerable<ExistingBooking> existing, Guid? ignoreAppointmentId = null)
    {
        var block = requested.BlockRange;
        var wanted = requested.AllResources().ToDictionary(r => r.Id, r => r.Type);
        return existing
            .Where(e => e.AppointmentId != ignoreAppointmentId && wanted.ContainsKey(e.ResourceId) && e.Block.Overlaps(block))
            .Select(e => new BookingConflict(e.ResourceId, wanted[e.ResourceId], e.AppointmentId, e.Block))
            .ToList();
    }
}
