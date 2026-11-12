using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>Bekleme listesi: customers waiting for a slot; matching entries are suggested when an appointment is cancelled.</summary>
public sealed class WaitlistService(AppServiceDeps deps) : AppServiceBase(deps), IWaitlistService
{
    public Task<Result<IReadOnlyList<WaitlistItem>>> ListAsync(bool includeClosed = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<WaitlistItem>>(Permission.AppointmentsView, async db =>
        {
            var q = db.Waitlist.AsNoTracking();
            if (!includeClosed)
            {
                q = q.Where(w => w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Contacted);
            }

            var entries = await q.OrderBy(w => w.FromDate).ThenBy(w => w.CreatedAtUtc).Take(500).ToListAsync(ct);
            return Result.Success(await ToItemsAsync(db, entries, ct));
        }, ct);

    public Task<Result<Guid>> AddAsync(Guid customerId, Guid? serviceId, Guid? staffMemberId, DateOnly from, DateOnly to, string? preferredTime, string? notes, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.AppointmentsManage, async db =>
        {
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (customer is null)
            {
                return Error.NotFound("Müşteri");
            }

            var created = WaitlistEntry.Create(customerId, serviceId, staffMemberId, from, to, preferredTime, notes);
            if (created.IsFailure)
            {
                return created.Error;
            }

            db.Waitlist.Add(created.Value);
            await Audit.AppendAsync(db, "waitlist.added", "Waitlist", created.Value.Id, $"Müşteri #{customer.Number} bekleme listesine eklendi", ct);
            return created.Value.Id;
        }, ct);

    public Task<Result<bool>> SetStatusAsync(Guid id, WaitlistStatus status, Guid? appointmentId = null, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.AppointmentsManage, async db =>
        {
            var entry = await db.Waitlist.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (entry is null)
            {
                return Error.NotFound("Bekleme kaydı");
            }

            var r = status switch
            {
                WaitlistStatus.Contacted => entry.MarkContacted(),
                WaitlistStatus.Booked when appointmentId is { } a => entry.MarkBooked(a),
                WaitlistStatus.Booked => entry.MarkBooked(Guid.Empty),
                WaitlistStatus.Cancelled => entry.Cancel(),
                _ => Result.Failure(Error.Validation("waitlist.status", "Geçersiz durum.")),
            };
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, "waitlist.status", "Waitlist", entry.Id, WaitlistEntry.StatusName(status), ct);
            return true;
        }, ct);

    /// <summary>Open waitlist entries that fit the slot freed by a (cancelled) appointment.</summary>
    public Task<Result<IReadOnlyList<WaitlistItem>>> FindMatchesForAsync(Guid appointmentId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<WaitlistItem>>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var appt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
            if (appt is null)
            {
                return Error.NotFound("Randevu");
            }

            var day = TimeZones.LocalDate(appt.StartUtc, zone);
            var open = await db.Waitlist.AsNoTracking()
                .Where(w => (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Contacted) && w.FromDate <= day && w.ToDate >= day && w.CustomerId != appt.CustomerId)
                .ToListAsync(ct);
            var matches = open.Where(w => w.Matches(appt.ServiceId, appt.StaffMemberId, day)).ToList();
            return Result.Success(await ToItemsAsync(db, matches, ct));
        }, ct);

    private static async Task<IReadOnlyList<WaitlistItem>> ToItemsAsync(VernukoDbContext db, List<WaitlistEntry> entries, CancellationToken ct)
    {
        var customerIds = entries.Select(e => e.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var services = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var staff = await db.Staff.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
        return entries.Select(e => new WaitlistItem(
            e.Id, e.CustomerId,
            customers.GetValueOrDefault(e.CustomerId)?.FullName ?? "?",
            customers.GetValueOrDefault(e.CustomerId)?.DisplayPhone,
            e.ServiceId, e.ServiceId is { } s ? services.GetValueOrDefault(s) : null,
            e.StaffMemberId, e.StaffMemberId is { } st ? staff.GetValueOrDefault(st) : null,
            e.FromDate, e.ToDate, e.PreferredTime, e.Notes, e.Status, e.CreatedAtUtc)).ToList();
    }
}
