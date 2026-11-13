using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed record SellGiftCardRequest(Guid BuyerCustomerId, long AmountMinor, PaymentMethod Method, string? RecipientName, int? ValidDays, string IdempotencyKey);

public sealed record RedeemGiftCardRequest(string Code, Guid CustomerId, long AmountMinor, Guid? AppointmentId, string IdempotencyKey);

public sealed record GiftCardEntryDto(DateTime AtUtc, GiftCardEntryKind Kind, long AmountMinor, string? Note)
{
    public string KindName => Kind switch
    {
        GiftCardEntryKind.Sale => "Satış",
        GiftCardEntryKind.Redeem => "Kullanım",
        GiftCardEntryKind.Restore => "Geri alındı",
        _ => "İptal",
    };
}

public sealed record GiftCardDto(Guid Id, string Code, long InitialMinor, long BalanceMinor, string BuyerName, string? RecipientName, DateTime SoldAtUtc, DateTime? ExpiresAtUtc, bool IsVoided, IReadOnlyList<GiftCardEntryDto> Entries)
{
    public string StatusText => IsVoided ? "İptal" : ExpiresAtUtc is { } e && e <= DateTime.UtcNow ? "Süresi doldu" : BalanceMinor == 0 ? "Bitti" : "Geçerli";
}

/// <summary>
/// Hediye kartı: selling records the money received (payment + an equal charge on the buyer, so the buyer owes
/// nothing and the till shows the cash); paying with the card records a "Hediye kartı" payment that moves no
/// money and lowers the card balance. Balances come from an append-only ledger.
/// </summary>
public sealed class GiftCardService(AppServiceDeps deps) : AppServiceBase(deps), IGiftCardService
{
    public Task<Result<GiftCardDto>> SellAsync(SellGiftCardRequest r, CancellationToken ct = default) =>
        WriteAsync<GiftCardDto>(Permission.PaymentsRecord, async db =>
        {
            var existing = await db.GiftCards.AsNoTracking().FirstOrDefaultAsync(c => c.IdempotencyKey == r.IdempotencyKey, ct);
            if (existing is not null)
            {
                return await ToDtoAsync(db, existing, ct);
            }

            if (r.Method == PaymentMethod.GiftCard)
            {
                return Error.Validation("giftcard.method", "Hediye kartı başka bir hediye kartıyla satılamaz.");
            }

            var buyer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.BuyerCustomerId, ct);
            if (buyer is null)
            {
                return Error.NotFound("Müşteri");
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var card = GiftCard.Create(r.AmountMinor, r.BuyerCustomerId, r.RecipientName, r.ValidDays, UtcNow, r.IdempotencyKey);
            if (card.IsFailure)
            {
                return card.Error;
            }

            var money = new Money(r.AmountMinor, settings.Currency);
            var charge = CustomerTransaction.Create(Branch.DefaultId, buyer.Id, TransactionKind.Charge, money, null, false, null, null, $"Hediye kartı {card.Value.Code}", UserId, UtcNow, r.IdempotencyKey + ":charge");
            var payment = CustomerTransaction.Create(Branch.DefaultId, buyer.Id, TransactionKind.Payment, money, r.Method, false, null, null, $"Hediye kartı {card.Value.Code} satışı", UserId, UtcNow, r.IdempotencyKey + ":pay");
            if (charge.IsFailure || payment.IsFailure)
            {
                return charge.IsFailure ? charge.Error : payment.Error;
            }

            db.GiftCards.Add(card.Value);
            db.Transactions.Add(charge.Value);
            db.Transactions.Add(payment.Value);
            db.GiftCardEntries.Add(GiftCardEntry.Of(card.Value.Id, GiftCardEntryKind.Sale, r.AmountMinor, buyer.Id, payment.Value.Id, UserId, UtcNow));
            await Audit.AppendAsync(db, "giftcard.sold", "GiftCard", card.Value.Id, $"Hediye kartı {card.Value.Code} satıldı: {money.ToDisplayString()}, müşteri #{buyer.Number}", ct);
            await db.SaveChangesAsync(ct);
            return await ToDtoAsync(db, card.Value, ct);
        }, ct);

    public Task<Result<GiftCardDto>> GetAsync(string code, CancellationToken ct = default) =>
        ReadAsync<GiftCardDto>(Permission.PaymentsView, async db =>
        {
            var normalized = GiftCard.NormalizeCode(code);
            var card = normalized.Length == 0 ? null : await db.GiftCards.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, ct);
            return card is null ? Error.NotFound("Hediye kartı") : await ToDtoAsync(db, card, ct);
        }, ct);

    public Task<Result<IReadOnlyList<GiftCardDto>>> ListAsync(bool includeFinished = false, CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<GiftCardDto>>(Permission.PaymentsView, async db =>
        {
            var cards = await db.GiftCards.AsNoTracking().OrderByDescending(c => c.CreatedAtUtc).Take(500).ToListAsync(ct);
            var list = new List<GiftCardDto>();
            foreach (var c in cards)
            {
                var dto = await ToDtoAsync(db, c, ct);
                if (includeFinished || (!dto.IsVoided && dto.BalanceMinor > 0))
                {
                    list.Add(dto);
                }
            }

            return list;
        }, ct);

    /// <summary>Pay (part of) a customer's bill with a card; no new money enters the till.</summary>
    public Task<Result<Guid>> RedeemAsync(RedeemGiftCardRequest r, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.PaymentsRecord, async db =>
        {
            var done = await db.Transactions.AsNoTracking().Where(t => t.IdempotencyKey == r.IdempotencyKey).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (done is { } id)
            {
                return id;
            }

            var normalized = GiftCard.NormalizeCode(r.Code);
            var card = normalized.Length == 0 ? null : await db.GiftCards.FirstOrDefaultAsync(c => c.Code == normalized, ct);
            if (card is null)
            {
                return new Error("giftcard.not_found", "Bu kodla bir hediye kartı bulunamadı.");
            }

            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CustomerId, ct);
            if (customer is null)
            {
                return Error.NotFound("Müşteri");
            }

            var balance = await db.GiftCardEntries.Where(e => e.GiftCardId == card.Id).SumAsync(e => e.AmountMinor, ct);
            var can = card.CanRedeem(r.AmountMinor, balance, UtcNow);
            if (can.IsFailure)
            {
                return can.Error;
            }

            var (settings, _) = await BusinessContext.LoadAsync(db, ct);
            var payment = CustomerTransaction.CreateGiftCardRedemption(Branch.DefaultId, customer.Id, new Money(r.AmountMinor, settings.Currency),
                r.AppointmentId, $"Hediye kartı {card.Code}", UserId, UtcNow, r.IdempotencyKey);
            if (payment.IsFailure)
            {
                return payment.Error;
            }

            db.Transactions.Add(payment.Value);
            db.GiftCardEntries.Add(GiftCardEntry.Of(card.Id, GiftCardEntryKind.Redeem, -r.AmountMinor, customer.Id, payment.Value.Id, UserId, UtcNow));
            await Audit.AppendAsync(db, "giftcard.redeemed", "GiftCard", card.Id, $"Hediye kartı {card.Code} ile {r.AmountMinor / 100m:N2} ödendi, müşteri #{customer.Number}", ct);
            return payment.Value.Id;
        }, ct);

    /// <summary>Cancels a card (e.g. lost, refunded in cash separately); the remaining balance is removed.</summary>
    public Task<Result<bool>> VoidAsync(Guid cardId, string reason, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.PaymentsReverse, async db =>
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Error.Validation("giftcard.reason", "İptal gerekçesi yazın.");
            }

            var card = await db.GiftCards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
            if (card is null || card.VoidedAtUtc is not null)
            {
                return Error.NotFound("Hediye kartı");
            }

            var balance = await db.GiftCardEntries.Where(e => e.GiftCardId == card.Id).SumAsync(e => e.AmountMinor, ct);
            card.MarkVoided(UtcNow);
            if (balance != 0)
            {
                db.GiftCardEntries.Add(GiftCardEntry.Of(card.Id, GiftCardEntryKind.Void, -balance, null, null, UserId, UtcNow, reason.Trim()));
            }

            await Audit.AppendAsync(db, "giftcard.voided", "GiftCard", card.Id, $"Hediye kartı {card.Code} iptal edildi ({balance / 100m:N2} kalan). Gerekçe: {reason.Trim()}", ct);
            return true;
        }, ct);

    private static async Task<GiftCardDto> ToDtoAsync(Persistence.VernukoDbContext db, GiftCard card, CancellationToken ct)
    {
        var entries = await db.GiftCardEntries.AsNoTracking().Where(e => e.GiftCardId == card.Id).OrderBy(e => e.CreatedAtUtc).ToListAsync(ct);
        var buyer = await db.Customers.AsNoTracking().Where(c => c.Id == card.BuyerCustomerId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefaultAsync(ct) ?? "?";
        return new GiftCardDto(card.Id, card.Code, card.InitialMinor, entries.Sum(e => e.AmountMinor), buyer.Trim(), card.RecipientName, card.CreatedAtUtc, card.ExpiresAtUtc, card.VoidedAtUtc is not null,
            entries.Select(e => new GiftCardEntryDto(e.CreatedAtUtc, e.Kind, e.AmountMinor, e.Note)).ToList());
    }
}
