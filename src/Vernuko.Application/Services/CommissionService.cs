using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>Staff commission rules and reports (prim). Rules are settings; reports are financial.</summary>
public sealed class CommissionService(AppServiceDeps deps) : AppServiceBase(deps)
{
    public Task<Result<IReadOnlyList<CommissionRuleDto>>> GetRulesAsync(CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<CommissionRuleDto>>(Permission.SettingsManage, async db =>
        {
            var rules = await db.CommissionRules.AsNoTracking().OrderBy(r => r.Basis).ThenBy(r => r.CreatedAtUtc).ToListAsync(ct);
            var staff = await db.Staff.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
            var services = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
            return rules.Select(r => new CommissionRuleDto(
                r.Id, r.Basis, r.StaffMemberId,
                r.StaffMemberId is { } s ? staff.GetValueOrDefault(s) ?? "?" : "Tüm personel",
                r.ItemId,
                r.ItemId is { } i ? services.GetValueOrDefault(i) ?? products.GetValueOrDefault(i) ?? "?" : r.Basis == CommissionBasis.ProductSold ? "Tüm ürünler" : "Tüm hizmetler",
                r.RateKind, r.PercentBasisPoints, r.FixedMinor, r.IsActive)).ToList();
        }, ct);

    public Task<Result<Guid>> SaveRuleAsync(CommissionRuleDto dto, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.SettingsManage, async db =>
        {
            var v = CommissionRule.Validate(dto.RateKind, dto.PercentBasisPoints, dto.FixedMinor);
            if (v.IsFailure)
            {
                return v.Error;
            }

            CommissionRule? rule;
            if (dto.Id == Guid.Empty)
            {
                rule = new CommissionRule(dto.Basis, dto.StaffMemberId, dto.ItemId, dto.RateKind, dto.PercentBasisPoints, dto.FixedMinor);
                db.CommissionRules.Add(rule);
            }
            else
            {
                rule = await db.CommissionRules.FirstOrDefaultAsync(r => r.Id == dto.Id, ct);
                if (rule is null)
                {
                    return Error.NotFound("Prim kuralı");
                }

                var r = rule.Update(dto.Basis, dto.StaffMemberId, dto.ItemId, dto.RateKind, dto.PercentBasisPoints, dto.FixedMinor);
                if (r.IsFailure)
                {
                    return r.Error;
                }
            }

            rule.SetActive(dto.IsActive);
            await Audit.AppendAsync(db, "commission_rule.saved", "CommissionRule", rule.Id, $"{CommissionRule.BasisName(rule.Basis)}: {rule.Describe()}", ct);
            return rule.Id;
        }, ct);

    /// <summary>Commission per staff member for a period (financial report).</summary>
    public Task<Result<CommissionReport>> GetReportAsync(DateOnly from, DateOnly toInclusive, CancellationToken ct = default) =>
        ReadAsync<CommissionReport>(Permission.ReportsFinancial, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var start = TimeZones.DayRange(from, zone).Start;
            var end = TimeZones.DayRange(toInclusive, zone).End;
            var entries = await db.CommissionEntries.AsNoTracking()
                .Where(e => e.OccurredAtUtc >= start && e.OccurredAtUtc < end)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(ct);
            var staff = await db.Staff.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.DisplayName, ct);
            var totals = entries.GroupBy(e => e.StaffMemberId)
                .Select(g => new StaffCommissionTotal(
                    g.Key,
                    staff.GetValueOrDefault(g.Key) ?? "?",
                    g.Where(e => e.Basis == CommissionBasis.ServiceDelivered).Sum(e => e.AmountMinor),
                    g.Where(e => e.Basis == CommissionBasis.PackageSold).Sum(e => e.AmountMinor),
                    g.Where(e => e.Basis == CommissionBasis.ProductSold).Sum(e => e.AmountMinor)))
                .OrderByDescending(t => t.TotalMinor)
                .ToList();
            var lines = entries.Select(e => new CommissionLine(e.OccurredAtUtc, staff.GetValueOrDefault(e.StaffMemberId) ?? "?", e.Basis, e.Description, e.BaseAmountMinor, e.AmountMinor)).ToList();
            return new CommissionReport(from, toInclusive, totals, lines);
        }, ct);
}
