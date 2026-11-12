using Microsoft.EntityFrameworkCore;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Packages;
using Vernuko.Persistence;

namespace Vernuko.Application.Services;

/// <summary>Read-side helpers shared by appointment, customer and package screens.</summary>
internal static class AppointmentQueries
{
    public static async Task<IReadOnlyList<AppointmentListItem>> LoadItemsAsync(VernukoDbContext db, IReadOnlyCollection<Guid> ids, TimeZoneInfo zone, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var appts = await db.Appointments.AsNoTracking().Include(a => a.Bookings).Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var customerIds = appts.Select(a => a.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone }).ToDictionaryAsync(c => c.Id, ct);
        var serviceIds = appts.Select(a => a.ServiceId).Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var staffIds = appts.Select(a => a.StaffMemberId).Distinct().ToList();
        var staff = await db.Staff.AsNoTracking().Where(s => staffIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
        var resourceIds = appts.SelectMany(a => a.Bookings).Where(b => b.ResourceType == BookingResourceType.Resource).Select(b => b.ResourceId).Distinct().ToList();
        var resources = await db.Resources.AsNoTracking().Where(r => resourceIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var labels = await SessionLabelsAsync(db, appts, ct);

        return appts
            .OrderBy(a => a.StartUtc)
            .Select(a =>
            {
                var c = customers.GetValueOrDefault(a.CustomerId);
                var resourceNames = string.Join(", ", a.Bookings.Where(b => b.ResourceType == BookingResourceType.Resource)
                    .Select(b => resources.GetValueOrDefault(b.ResourceId)).Where(n => n is not null));
                return new AppointmentListItem(
                    a.Id,
                    TimeZones.ToLocal(a.StartUtc, zone),
                    TimeZones.ToLocal(a.EndUtc, zone),
                    a.CustomerId,
                    c is null ? "?" : string.IsNullOrEmpty(c.LastName) ? c.FirstName : $"{c.FirstName} {c.LastName}",
                    CustomerService.FormatPhone(c?.Phone),
                    a.ServiceId,
                    services.GetValueOrDefault(a.ServiceId) ?? "?",
                    labels.GetValueOrDefault(a.Id),
                    a.BodyArea,
                    a.StaffMemberId,
                    staff.GetValueOrDefault(a.StaffMemberId) ?? "?",
                    resourceNames.Length == 0 ? null : resourceNames,
                    a.Status,
                    a.Notes,
                    a.CustomerPackageId,
                    a.PriceMinor);
            })
            .ToList();
    }

    /// <summary>"3. Seans" labels: position among the package's non-cancelled appointments (+ sessions used before Vernuko).</summary>
    private static async Task<Dictionary<Guid, string>> SessionLabelsAsync(VernukoDbContext db, IReadOnlyList<Appointment> appts, CancellationToken ct)
    {
        var packageIds = appts.Where(a => a.CustomerPackageId is not null).Select(a => a.CustomerPackageId!.Value).Distinct().ToList();
        var result = new Dictionary<Guid, string>();
        if (packageIds.Count == 0)
        {
            return result;
        }

        var siblings = await db.Appointments.AsNoTracking()
            .Where(a => a.CustomerPackageId != null && packageIds.Contains(a.CustomerPackageId.Value))
            .Select(a => new { a.Id, a.CustomerPackageId, a.StartUtc, a.Status })
            .ToListAsync(ct);
        var adjustments = await db.SessionLedger.AsNoTracking()
            .Where(e => packageIds.Contains(e.CustomerPackageId) && e.Kind == SessionEntryKind.Adjustment)
            .GroupBy(e => e.CustomerPackageId)
            .Select(g => new { g.Key, Sum = g.Sum(e => e.Delta) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        foreach (var a in appts.Where(a => a.CustomerPackageId is not null))
        {
            var pid = a.CustomerPackageId!.Value;
            if (!AppointmentStatusRules.HoldsResources(a.Status))
            {
                continue;
            }

            var prior = siblings.Count(s => s.CustomerPackageId == pid && s.Id != a.Id && s.StartUtc < a.StartUtc && AppointmentStatusRules.HoldsResources(s.Status));
            var preUsed = Math.Max(0, -adjustments.GetValueOrDefault(pid));
            result[a.Id] = $"{prior + preUsed + 1}. Seans";
        }

        return result;
    }

    /// <summary>Loads the calendar situation around one local day for availability checks.</summary>
    public static async Task<DayContext> LoadDayContextAsync(VernukoDbContext db, BusinessSettings settings, TimeZoneInfo zone, DateOnly day, Guid staffId, IReadOnlyCollection<Guid> resourceIds, CancellationToken ct)
    {
        var range = TimeZones.DayRange(day, zone);
        var from = range.Start.AddDays(-1);
        var to = range.End.AddDays(1);
        var ids = resourceIds.Append(staffId).Distinct().ToList();
        var bookings = await db.AppointmentBookings.AsNoTracking()
            .Where(b => ids.Contains(b.ResourceId) && b.IsActive && b.BlockStartUtc < to && b.BlockEndUtc > from)
            .Select(b => new { b.AppointmentId, b.ResourceId, b.BlockStartUtc, b.BlockEndUtc })
            .ToListAsync(ct);
        var exceptions = await db.ScheduleExceptions.AsNoTracking()
            .Where(e => e.StartUtc < to && e.EndUtc > from)
            .ToListAsync(ct);
        var staffHours = await db.Staff.AsNoTracking().Where(s => s.Id == staffId).Select(s => s.WorkingHours).FirstOrDefaultAsync(ct);
        return new DayContext(
            day,
            zone,
            settings.BusinessHours,
            staffHours,
            exceptions,
            bookings.Select(b => new ExistingBooking(b.AppointmentId, b.ResourceId, new UtcRange(b.BlockStartUtc, b.BlockEndUtc))).ToList());
    }
}
