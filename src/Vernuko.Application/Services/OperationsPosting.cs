using Microsoft.EntityFrameworkCore;
using Vernuko.Appointments;
using Vernuko.Inventory;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Persistence;

namespace Vernuko.Application.Services;

/// <summary>
/// Posts commissions and stock movements inside the caller's transaction. Every posting is idempotent per source
/// (an appointment completed twice is commissioned once) and every correction is a reversal row.
/// </summary>
internal static class OperationsPosting
{
    public static async Task OnAppointmentCompletedAsync(VernukoDbContext db, Appointment appt, CustomerPackage? package, Guid? userId, DateTime now, CancellationToken ct)
    {
        // Commission for the staff member who delivered the service.
        if ((await OpenCommissionsAsync(db, appt.Id, CommissionBasis.ServiceDelivered, ct)).Count == 0)
        {
            var rules = await db.CommissionRules.AsNoTracking().Where(r => r.IsActive && r.Basis == CommissionBasis.ServiceDelivered).ToListAsync(ct);
            var rule = CommissionRules.Resolve(rules, CommissionBasis.ServiceDelivered, appt.StaffMemberId, appt.ServiceId);
            var baseAmount = package is not null ? package.SessionValueMinor : appt.PriceMinor;
            if (rule is not null && rule.Calculate(baseAmount) != 0)
            {
                db.CommissionEntries.Add(CommissionEntry.Create(appt.StaffMemberId, CommissionBasis.ServiceDelivered, appt.Id, baseAmount, rule, 1,
                    package is null ? "Hizmet uygulama" : "Paket seansı uygulama", now));
            }
        }

        // Materials used by the service.
        var consumed = await db.StockMovements.AsNoTracking()
            .Where(m => m.AppointmentId == appt.Id && m.Kind == StockMovementKind.Consumed)
            .Select(m => m.Id).ToListAsync(ct);
        var reversed = await db.StockMovements.AsNoTracking()
            .Where(m => m.ReversesMovementId != null && consumed.Contains(m.ReversesMovementId.Value))
            .CountAsync(ct);
        if (consumed.Count - reversed > 0)
        {
            return;
        }

        var materials = await db.ServiceMaterials.AsNoTracking().Where(m => m.ServiceId == appt.ServiceId).ToListAsync(ct);
        foreach (var material in materials)
        {
            var movement = StockMovement.Create(material.ProductId, StockMovementKind.Consumed, material.Quantity, null, appt.Id, appt.CustomerId, "Hizmette otomatik kullanım", userId, now);
            if (movement.IsSuccess)
            {
                db.StockMovements.Add(movement.Value);
            }
        }
    }

    public static async Task OnAppointmentReopenedAsync(VernukoDbContext db, Guid appointmentId, Guid? userId, DateTime now, string reason, CancellationToken ct)
    {
        await ReverseCommissionsAsync(db, appointmentId, CommissionBasis.ServiceDelivered, reason, now, ct);

        var consumed = await db.StockMovements.AsNoTracking()
            .Where(m => m.AppointmentId == appointmentId && m.Kind == StockMovementKind.Consumed)
            .ToListAsync(ct);
        var ids = consumed.Select(m => (Guid?)m.Id).ToList();
        var reversed = await db.StockMovements.AsNoTracking().Where(m => ids.Contains(m.ReversesMovementId)).Select(m => m.ReversesMovementId).ToListAsync(ct);
        foreach (var m in consumed.Where(m => !reversed.Contains(m.Id)))
        {
            db.StockMovements.Add(StockMovement.Reverse(m, reason, userId, now));
        }
    }

    public static async Task PostSaleCommissionAsync(VernukoDbContext db, CommissionBasis basis, Guid sourceId, Guid staffMemberId, Guid itemId, long baseAmountMinor, int quantity, string description, DateTime now, CancellationToken ct)
    {
        var rules = await db.CommissionRules.AsNoTracking().Where(r => r.IsActive && r.Basis == basis).ToListAsync(ct);
        var rule = CommissionRules.Resolve(rules, basis, staffMemberId, itemId);
        if (rule is not null && rule.Calculate(baseAmountMinor, quantity) != 0)
        {
            db.CommissionEntries.Add(CommissionEntry.Create(staffMemberId, basis, sourceId, baseAmountMinor, rule, quantity, description, now));
        }
    }

    public static async Task ReverseCommissionsAsync(VernukoDbContext db, Guid sourceId, CommissionBasis basis, string reason, DateTime now, CancellationToken ct)
    {
        foreach (var entry in await OpenCommissionsAsync(db, sourceId, basis, ct))
        {
            db.CommissionEntries.Add(CommissionEntry.Reverse(entry, reason, now));
        }
    }

    private static async Task<List<CommissionEntry>> OpenCommissionsAsync(VernukoDbContext db, Guid sourceId, CommissionBasis basis, CancellationToken ct)
    {
        var entries = await db.CommissionEntries.AsNoTracking()
            .Where(e => e.SourceId == sourceId && e.Basis == basis && e.ReversesEntryId == null)
            .ToListAsync(ct);
        var ids = entries.Select(e => (Guid?)e.Id).ToList();
        var reversed = await db.CommissionEntries.AsNoTracking().Where(e => ids.Contains(e.ReversesEntryId)).Select(e => e.ReversesEntryId).ToListAsync(ct);
        return entries.Where(e => !reversed.Contains(e.Id)).ToList();
    }
}
