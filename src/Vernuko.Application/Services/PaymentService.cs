using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

/// <summary>Cash desk and customer accounts. Records only — not an invoicing or e-Fatura system.</summary>
public sealed class PaymentService(AppServiceDeps deps) : AppServiceBase(deps), IPaymentService
{
    /// <summary>
    /// Records a payment/refund/discount/charge. Idempotent: the same <see cref="RecordTransactionRequest.IdempotencyKey"/>
    /// (generated once per dialog) never creates a second record, even after a double click or retry.
    /// </summary>
    public Task<Result<Guid>> RecordAsync(RecordTransactionRequest r, CancellationToken ct = default) =>
        WriteAsync<Guid>(r.Kind is TransactionKind.Refund or TransactionKind.Discount or TransactionKind.Charge ? Permission.PaymentsReverse : Permission.PaymentsRecord, async db =>
        {
            var existing = await db.Transactions.AsNoTracking().Where(t => t.IdempotencyKey == r.IdempotencyKey).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (existing is { } id)
            {
                return id;
            }

            if (r.Method == PaymentMethod.GiftCard)
            {
                return Error.Validation("transaction.gift_card", "Hediye kartıyla ödeme kart koduyla alınır (bakiye kartından düşülür).");
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
            if (customer is null)
            {
                return Error.NotFound("Müşteri");
            }

            var created = CustomerTransaction.Create(Branch.DefaultId, r.CustomerId, r.Kind, new Money(r.AmountMinor, settings.Currency), r.Method, r.IsDeposit,
                r.AppointmentId, r.CustomerPackageId, r.Description, UserId, UtcNow, r.IdempotencyKey);
            if (created.IsFailure)
            {
                return created.Error;
            }

            db.Transactions.Add(created.Value);
            await Audit.AppendAsync(db, "transaction.recorded", "CustomerTransaction", created.Value.Id,
                $"{CustomerTransaction.KindName(r.Kind)} {new Money(r.AmountMinor, settings.Currency).ToDisplayString()} ({CustomerTransaction.MethodName(r.Method)}), müşteri #{customer.Number}", ct);
            return created.Value.Id;
        }, ct);

    /// <summary>Corrects a wrong entry with a reversal record (financial history is never edited or deleted).</summary>
    public Task<Result<Guid>> ReverseAsync(Guid transactionId, string reason, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.PaymentsReverse, async db =>
        {
            var original = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == transactionId, ct);
            if (original is null)
            {
                return Error.NotFound("İşlem");
            }

            if (await db.Transactions.AnyAsync(t => t.ReversesTransactionId == transactionId, ct))
            {
                return Error.Validation("transaction.already_reversed", "Bu işlem zaten düzeltilmiş.");
            }

            var reversal = CustomerTransaction.Reverse(original, reason, UserId, UtcNow);
            if (reversal.IsFailure)
            {
                return reversal.Error;
            }

            db.Transactions.Add(reversal.Value);
            if (original.Method == PaymentMethod.GiftCard && original.Kind == TransactionKind.Payment)
            {
                // The wrong gift card payment is undone: the amount goes back onto the card.
                var used = await db.GiftCardEntries.AsNoTracking().FirstOrDefaultAsync(e => e.TransactionId == original.Id && e.Kind == GiftCardEntryKind.Redeem, ct);
                if (used is not null)
                {
                    db.GiftCardEntries.Add(GiftCardEntry.Of(used.GiftCardId, GiftCardEntryKind.Restore, -used.AmountMinor, original.CustomerId, reversal.Value.Id, UserId, UtcNow, reason.Trim()));
                }
            }

            await Audit.AppendAsync(db, "transaction.reversed", "CustomerTransaction", original.Id, $"Ters kayıt oluşturuldu. Gerekçe: {reason.Trim()}", ct);
            return reversal.Value.Id;
        }, ct);

    public Task<Result<long>> GetBalanceAsync(Guid customerId, CancellationToken ct = default) =>
        ReadAsync<long>(Permission.PaymentsView, async db => await db.Transactions.Where(t => t.CustomerId == customerId).SumAsync(t => t.BalanceEffectMinor, ct), ct);

    public Task<Result<IReadOnlyList<TransactionDto>>> GetCustomerTransactionsAsync(Guid customerId, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<TransactionDto>>(Permission.PaymentsView, async db =>
            Result.Success(await LoadAsync(db, db.Transactions.AsNoTracking().Where(t => t.CustomerId == customerId), ct)), ct);

    public Task<Result<CashDaySummary>> GetCashDayAsync(DateOnly day, CancellationToken ct = default) =>
        ReadAsync<CashDaySummary>(Permission.PaymentsView, async db =>
        {
            var (_, zone) = await BusinessContext.LoadAsync(db, ct);
            var range = TimeZones.DayRange(day, zone);
            var entries = await db.Transactions.AsNoTracking().Where(t => t.OccurredAtUtc >= range.Start && t.OccurredAtUtc < range.End).ToListAsync(ct);
            var byMethod = AccountMath.CashByMethod(entries);
            var dtos = await LoadAsync(db, db.Transactions.AsNoTracking().Where(t => t.OccurredAtUtc >= range.Start && t.OccurredAtUtc < range.End), ct);
            return new CashDaySummary(day, byMethod, byMethod.Values.Sum(), dtos);
        }, ct);

    /// <summary>Open balances across all customers — a financial report (owner/accounting by default).</summary>
    public Task<Result<IReadOnlyList<DebtorDto>>> GetDebtorsAsync(CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<DebtorDto>>(Permission.ReportsFinancial, async db =>
        {
            var balances = await db.Transactions.AsNoTracking().GroupBy(t => t.CustomerId)
                .Select(g => new { CustomerId = g.Key, Balance = g.Sum(t => t.BalanceEffectMinor) })
                .Where(x => x.Balance > 0)
                .OrderByDescending(x => x.Balance)
                .Take(500)
                .ToListAsync(ct);
            var ids = balances.Select(b => b.CustomerId).ToList();
            var customers = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
            return balances.Select(b => new DebtorDto(b.CustomerId, customers.GetValueOrDefault(b.CustomerId)?.FullName ?? "?",
                customers.GetValueOrDefault(b.CustomerId)?.DisplayPhone, b.Balance)).ToList();
        }, ct);

    private static async Task<IReadOnlyList<TransactionDto>> LoadAsync(VernukoDbContext db, IQueryable<CustomerTransaction> query, CancellationToken ct)
    {
        var rows = await query.OrderByDescending(t => t.OccurredAtUtc).Take(1000).ToListAsync(ct);
        var ids = rows.Select(r => (Guid?)r.Id).ToList();
        var reversed = (await db.Transactions.AsNoTracking().Where(t => ids.Contains(t.ReversesTransactionId)).Select(t => t.ReversesTransactionId).ToListAsync(ct)).ToHashSet();
        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
        return rows.Select(t => new TransactionDto(t.Id, t.CustomerId, customers.GetValueOrDefault(t.CustomerId) ?? "?", t.Kind, t.AmountMinor, t.BalanceEffectMinor,
            t.Method, t.IsDeposit, t.Description, t.OccurredAtUtc, t.ReversesTransactionId, reversed.Contains(t.Id))).ToList();
    }
}
