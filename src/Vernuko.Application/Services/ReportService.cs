using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Reporting;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed class ReportService(AppServiceDeps deps) : AppServiceBase(deps)
{
    public Task<Result<DailySummary>> GetDailySummaryAsync(DateOnly day, CancellationToken ct = default) =>
        ReadAsync<DailySummary>(Permission.AppointmentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var range = TimeZones.DayRange(day, zone);
            var statuses = await db.Appointments.AsNoTracking().Where(a => a.StartUtc >= range.Start && a.StartUtc < range.End).Select(a => a.Status).ToListAsync(ct);
            return DailySummary.From(statuses);
        }, ct);

    /// <summary>Operational figures for everyone with report access; money figures only with the financial permission.</summary>
    public Task<Result<PeriodReport>> GetPeriodReportAsync(DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        ReadAsync<PeriodReport>(Permission.ReportsOperational, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = TimeZones.DayRange(from, zone).Start;
            var end = TimeZones.DayRange(toInclusive, zone).End;
            var appts = await db.Appointments.AsNoTracking().Where(a => a.StartUtc >= start && a.StartUtc < end)
                .Select(a => new { a.Status, a.StaffMemberId, a.ServiceId }).ToListAsync(ct);
            var staff = await db.Staff.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
            var services = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            var rates = AppointmentRates.From(appts.Select(a => a.Status));
            var delivered = appts.Where(a => a.Status == AppointmentStatus.Completed).ToList();
            var byStaff = delivered.GroupBy(a => a.StaffMemberId).Select(g => (staff.GetValueOrDefault(g.Key) ?? "?", g.Count())).OrderByDescending(x => x.Item2).ToList();
            var byService = delivered.GroupBy(a => a.ServiceId).Select(g => (services.GetValueOrDefault(g.Key) ?? "?", g.Count())).OrderByDescending(x => x.Item2).ToList();
            var packagesSold = await db.CustomerPackages.CountAsync(p => p.PurchasedAtUtc >= start && p.PurchasedAtUtc < end, ct);
            var consumed = await db.SessionLedger.Where(e => e.AtUtc >= start && e.AtUtc < end && e.Kind != SessionEntryKind.Adjustment).SumAsync(e => -e.Delta, ct);

            long? collected = null;
            long? outstanding = null;
            if (Session.Has(Permission.ReportsFinancial))
            {
                var tx = await db.Transactions.AsNoTracking().Where(t => t.OccurredAtUtc >= start && t.OccurredAtUtc < end).ToListAsync(ct);
                collected = AccountMath.CashByMethod(tx).Values.Sum();
                outstanding = await db.Transactions.SumAsync(t => t.BalanceEffectMinor, ct);
            }

            return new PeriodReport(from, toInclusive, appts.Count, delivered.Count,
                appts.Count(a => a.Status == AppointmentStatus.Cancelled), appts.Count(a => a.Status == AppointmentStatus.NoShow),
                rates.CancellationRate, rates.NoShowRate, byStaff, byService, packagesSold, consumed, collected, outstanding);
        }, ct);
}

public sealed record AuditEntryDto(long Sequence, DateTime OccurredAtUtc, string? UserName, string Action, string EntityType, string Summary);

public sealed record AuditVerification(bool IsIntact, long CheckedEntries, long? FirstBrokenSequence);

public sealed class AuditService(AppServiceDeps deps) : AppServiceBase(deps)
{
    public Task<Result<IReadOnlyList<AuditEntryDto>>> GetRecentAsync(int take, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<AuditEntryDto>>(Permission.AuditLogView, async db =>
            await db.AuditEntries.AsNoTracking().OrderByDescending(a => a.Sequence).Take(Math.Clamp(take, 1, 2000))
                .Select(a => new AuditEntryDto(a.Sequence, a.OccurredAtUtc, a.UserName, a.Action, a.EntityType, a.Summary)).ToListAsync(ct), ct);

    /// <summary>Re-computes the hash chain; any edited or deleted entry breaks it.</summary>
    public Task<Result<AuditVerification>> VerifyChainAsync(CancellationToken ct = default) =>
        ReadAsync<AuditVerification>(Permission.AuditLogView, async db =>
        {
            var previous = HashChain.Genesis;
            long count = 0;
            long expectedSequence = 1;
            await foreach (var entry in db.AuditEntries.AsNoTracking().OrderBy(a => a.Sequence).AsAsyncEnumerable().WithCancellation(ct))
            {
                if (entry.Sequence != expectedSequence || !entry.IsIntact(previous))
                {
                    return new AuditVerification(false, count, entry.Sequence);
                }

                previous = entry.Hash;
                expectedSequence++;
                count++;
            }

            return new AuditVerification(true, count, null);
        }, ct);
}
