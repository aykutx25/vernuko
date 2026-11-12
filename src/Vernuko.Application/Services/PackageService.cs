using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed class PackageService(AppServiceDeps deps) : AppServiceBase(deps), IPackageService
{
    /// <summary>Sells a package: creates the package, the receivable (charge) and an optional initial payment atomically.</summary>
    public Task<Result<Guid>> SellAsync(SellPackageRequest r, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.PackagesSell, async db =>
        {
            if (await db.Transactions.AnyAsync(t => t.IdempotencyKey == $"package-sale:{r.IdempotencyKey}", ct))
            {
                return await db.Transactions.Where(t => t.IdempotencyKey == $"package-sale:{r.IdempotencyKey}").Select(t => t.CustomerPackageId!.Value).FirstAsync(ct);
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
            if (customer is null || customer.State != CustomerState.Active)
            {
                return Error.Validation("package.customer", "Müşteri bulunamadı veya aktif değil.");
            }

            if (!await db.Services.AnyAsync(s => s.Id == r.ServiceId, ct))
            {
                return Error.Validation("package.service", "Hizmet seçilmelidir.");
            }

            if (r.InitialPaymentMinor < 0 || r.InitialPaymentMinor > r.PriceMinor)
            {
                return Error.Validation("package.payment", "Peşinat, paket fiyatından büyük olamaz.");
            }

            if (r.SoldByStaffId is { } seller && !await db.Staff.AnyAsync(s => s.Id == seller, ct))
            {
                return Error.Validation("package.seller", "Satışı yapan personel bulunamadı.");
            }

            var sold = CustomerPackage.Sell(r.CustomerId, r.ServiceId, r.TemplateId, r.Name, r.BodyArea, r.Sessions, r.PriceMinor, r.ValidityDays, UtcNow, r.SoldByStaffId);
            if (sold.IsFailure)
            {
                return sold.Error;
            }

            var package = sold.Value;
            db.CustomerPackages.Add(package);
            if (r.PriceMinor > 0)
            {
                var charge = CustomerTransaction.Create(Branch.DefaultId, r.CustomerId, TransactionKind.Charge, new Money(r.PriceMinor, settings.Currency), null, false,
                    null, package.Id, $"Paket satışı: {package.Name}", UserId, UtcNow, $"package-sale:{r.IdempotencyKey}");
                if (charge.IsFailure)
                {
                    return charge.Error;
                }

                db.Transactions.Add(charge.Value);
            }

            if (r.InitialPaymentMinor > 0)
            {
                if (r.PaymentMethod is null)
                {
                    return Error.Validation("package.method", "Peşinat için ödeme yöntemi seçilmelidir.");
                }

                var pay = CustomerTransaction.Create(Branch.DefaultId, r.CustomerId, TransactionKind.Payment, new Money(r.InitialPaymentMinor, settings.Currency), r.PaymentMethod, false,
                    null, package.Id, $"Paket ödemesi: {package.Name}", UserId, UtcNow, $"package-payment:{r.IdempotencyKey}");
                if (pay.IsFailure)
                {
                    return pay.Error;
                }

                db.Transactions.Add(pay.Value);
            }

            if (r.SoldByStaffId is { } staffId)
            {
                await OperationsPosting.PostSaleCommissionAsync(db, Payments.CommissionBasis.PackageSold, package.Id, staffId, package.ServiceId, package.PriceMinor, 1, $"Paket satışı: {package.Name}", UtcNow, ct);
            }

            await Audit.AppendAsync(db, "package.sold", "CustomerPackage", package.Id, $"Müşteri #{customer.Number}: {package.Name}, {package.TotalSessions} seans", ct);
            return package.Id;
        }, ct);

    public Task<Result<IReadOnlyList<CustomerPackageDto>>> GetCustomerPackagesAsync(Guid customerId, bool activeOnly = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<CustomerPackageDto>>(Permission.PackagesView, db => ListAsync(db, p => p.CustomerId == customerId, activeOnly, ct), ct);

    public Task<Result<IReadOnlyList<CustomerPackageDto>>> SearchAsync(string? customerQuery, bool activeOnly, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<CustomerPackageDto>>(Permission.PackagesView, async db =>
        {
            if (string.IsNullOrWhiteSpace(customerQuery))
            {
                return await ListAsync(db, _ => true, activeOnly, ct);
            }

            var key = "%" + TurkishText.ToSearchKey(customerQuery) + "%";
            var ids = await db.Customers.AsNoTracking().Where(c => EF.Functions.Like(c.SearchKey, key)).Select(c => c.Id).Take(200).ToListAsync(ct);
            return await ListAsync(db, p => ids.Contains(p.CustomerId), activeOnly, ct);
        }, ct);

    public Task<Result<IReadOnlyList<SessionLedgerDto>>> GetLedgerAsync(Guid packageId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<SessionLedgerDto>>(Permission.PackagesView, async db =>
        {
            var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
            return (await db.SessionLedger.AsNoTracking().Where(e => e.CustomerPackageId == packageId).OrderBy(e => e.AtUtc).ToListAsync(ct))
                .Select(e => new SessionLedgerDto(e.AtUtc, e.Kind, e.Delta, e.AppointmentId, e.Reason, e.UserId is { } u ? users.GetValueOrDefault(u) : null))
                .ToList();
        }, ct);

    public Task<Result<int>> AdjustSessionsAsync(Guid packageId, int delta, string reason, CancellationToken ct = default) =>
        WriteAsync<int>(Permission.PackagesAdjustSessions, async db =>
        {
            var p = await db.CustomerPackages.Include(x => x.Entries).FirstOrDefaultAsync(x => x.Id == packageId, ct);
            if (p is null)
            {
                return Error.NotFound("Paket");
            }

            var before = p.RemainingSessions;
            var r = p.Adjust(delta, reason, UserId, UtcNow);
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, "package.adjusted", "CustomerPackage", p.Id, $"Kalan seans {before} → {p.RemainingSessions}. Gerekçe: {reason.Trim()}", ct);
            return p.RemainingSessions;
        }, ct);

    public Task<Result<bool>> FreezeAsync(Guid packageId, bool freeze, CancellationToken ct = default) =>
        Mutate(packageId, freeze ? "package.frozen" : "package.unfrozen", p => freeze ? p.Freeze(UtcNow) : p.Unfreeze(UtcNow), freeze ? "Paket donduruldu" : "Paket aktifleştirildi", ct);

    public Task<Result<bool>> ExtendAsync(Guid packageId, int days, CancellationToken ct = default) =>
        Mutate(packageId, "package.extended", p => p.Extend(days), $"Geçerlilik {days} gün uzatıldı", ct);

    /// <summary>Cancels a package; the seller's sales commission is reversed. Refunds are recorded separately in payments.</summary>
    public Task<Result<bool>> CancelAsync(Guid packageId, string reason, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.PackagesAdjustSessions, async db =>
        {
            var p = await db.CustomerPackages.Include(x => x.Entries).FirstOrDefaultAsync(x => x.Id == packageId, ct);
            if (p is null)
            {
                return Error.NotFound("Paket");
            }

            var r = p.Cancel(reason);
            if (r.IsFailure)
            {
                return r.Error;
            }

            await OperationsPosting.ReverseCommissionsAsync(db, p.Id, Payments.CommissionBasis.PackageSold, "Paket iptal edildi", UtcNow, ct);
            await Audit.AppendAsync(db, "package.cancelled", "CustomerPackage", p.Id, "Paket iptal edildi (iade ayrıca ödeme ekranından kaydedilir)", ct);
            return true;
        }, ct);

    private Task<Result<bool>> Mutate(Guid packageId, string action, Func<CustomerPackage, Result> change, string summary, CancellationToken ct) =>
        WriteAsync<bool>(Permission.PackagesAdjustSessions, async db =>
        {
            var p = await db.CustomerPackages.Include(x => x.Entries).FirstOrDefaultAsync(x => x.Id == packageId, ct);
            if (p is null)
            {
                return Error.NotFound("Paket");
            }

            var r = change(p);
            if (r.IsFailure)
            {
                return r.Error;
            }

            await Audit.AppendAsync(db, action, "CustomerPackage", p.Id, summary, ct);
            return true;
        }, ct);

    private static async Task<Result<IReadOnlyList<CustomerPackageDto>>> ListAsync(VernukoDbContext db, System.Linq.Expressions.Expression<Func<CustomerPackage, bool>> filter, bool activeOnly, CancellationToken ct)
    {
        var q = db.CustomerPackages.AsNoTracking().Include(p => p.Entries).Where(filter);
        if (activeOnly)
        {
            q = q.Where(p => p.Status == PackageStatus.Active);
        }

        var packages = await q.OrderByDescending(p => p.PurchasedAtUtc).Take(500).ToListAsync(ct);
        var customerIds = packages.Select(p => p.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        var services = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var packageIds = packages.Select(p => (Guid?)p.Id).ToList();
        var open = new[] { AppointmentStatus.Created, AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.Waiting, AppointmentStatus.InProgress };
        var upcoming = await db.Appointments.AsNoTracking().Where(a => packageIds.Contains(a.CustomerPackageId) && open.Contains(a.Status))
            .GroupBy(a => a.CustomerPackageId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key!.Value, x => x.Count, ct);
        return packages.Select(p => new CustomerPackageDto(
            p.Id, p.CustomerId, customers.GetValueOrDefault(p.CustomerId) ?? "?", p.ServiceId, services.GetValueOrDefault(p.ServiceId) ?? "?",
            p.Name, p.BodyArea, p.TotalSessions, p.UsedSessions, p.RemainingSessions, upcoming.GetValueOrDefault(p.Id),
            p.PurchasedAtUtc, p.ExpiresAtUtc, p.Status, p.PriceMinor)).ToList();
    }
}
