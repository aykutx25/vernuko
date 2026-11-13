using Vernuko.Core;

namespace Vernuko.Payments;

public enum TransactionKind
{
    /// <summary>Customer owes money (service delivered, package sold).</summary>
    Charge,

    /// <summary>Money received from the customer.</summary>
    Payment,

    /// <summary>Money returned to the customer.</summary>
    Refund,

    /// <summary>Price reduction granted to the customer.</summary>
    Discount,

    /// <summary>Cancels the effect of an earlier entry (financial records are never deleted or edited).</summary>
    Reversal,
}

public enum PaymentMethod
{
    Cash,
    Card,
    BankTransfer,
    Other,

    /// <summary>Paid with a gift card: no new money (it came in when the card was sold).</summary>
    GiftCard,
}

/// <summary>
/// Append-only customer account ledger. This is internal bookkeeping only; it is NOT an official invoice,
/// e-Fatura or e-Arşiv document.
/// </summary>
public sealed class CustomerTransaction
{
    private CustomerTransaction()
    {
    }

    private CustomerTransaction(Guid customerId, TransactionKind kind, long amountMinor, string currency, DateTime occurredAtUtc, string idempotencyKey)
    {
        Id = Ids.New();
        CustomerId = customerId;
        Kind = kind;
        AmountMinor = amountMinor;
        Currency = currency;
        OccurredAtUtc = occurredAtUtc;
        IdempotencyKey = idempotencyKey;
    }

    public Guid Id { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid CustomerId { get; private set; }

    public TransactionKind Kind { get; private set; }

    /// <summary>Always positive; the sign of the balance effect comes from <see cref="Kind"/>.</summary>
    public long AmountMinor { get; private set; }

    public string Currency { get; private set; } = "TRY";

    public PaymentMethod? Method { get; private set; }

    /// <summary>Kapora (advance payment) flag for payments.</summary>
    public bool IsDeposit { get; private set; }

    public Guid? AppointmentId { get; private set; }

    public Guid? CustomerPackageId { get; private set; }

    public Guid? ReversesTransactionId { get; private set; }

    public string? Description { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? RecordedByUserId { get; private set; }

    /// <summary>Unique key that makes a double-click / retry harmless.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>
    /// Stored effect on what the customer owes (+ increases debt, − decreases it). Persisted so balances can be
    /// summed in SQL. For reversal rows it is the negation of the reversed entry's effect.
    /// </summary>
    public long BalanceEffectMinor { get; private set; }

    private static long EffectOf(TransactionKind kind, long amount) => kind switch
    {
        TransactionKind.Charge => amount,
        TransactionKind.Refund => amount,
        TransactionKind.Payment => -amount,
        TransactionKind.Discount => -amount,
        _ => 0,
    };

    public static Result<CustomerTransaction> Create(
        Guid branchId,
        Guid customerId,
        TransactionKind kind,
        Money amount,
        PaymentMethod? method,
        bool isDeposit,
        Guid? appointmentId,
        Guid? packageId,
        string? description,
        Guid? userId,
        DateTime nowUtc,
        string idempotencyKey)
    {
        if (method == PaymentMethod.GiftCard)
        {
            return Error.Validation("transaction.gift_card", "Hediye kartıyla ödeme kart koduyla alınır (bakiye kartından düşülür).");
        }

        return CreateCore(branchId, customerId, kind, amount, method, isDeposit, appointmentId, packageId, description, userId, nowUtc, idempotencyKey);
    }

    /// <summary>The only way to record a "Hediye kartı" payment: the caller lowers the card balance in the same transaction.</summary>
    public static Result<CustomerTransaction> CreateGiftCardRedemption(Guid branchId, Guid customerId, Money amount, Guid? appointmentId, string description, Guid? userId, DateTime nowUtc, string idempotencyKey) =>
        CreateCore(branchId, customerId, TransactionKind.Payment, amount, PaymentMethod.GiftCard, false, appointmentId, null, description, userId, nowUtc, idempotencyKey);

    private static Result<CustomerTransaction> CreateCore(
        Guid branchId,
        Guid customerId,
        TransactionKind kind,
        Money amount,
        PaymentMethod? method,
        bool isDeposit,
        Guid? appointmentId,
        Guid? packageId,
        string? description,
        Guid? userId,
        DateTime nowUtc,
        string idempotencyKey)
    {
        if (kind == TransactionKind.Reversal)
        {
            return Error.Validation("transaction.kind", "Ters kayıt yalnızca Reverse ile oluşturulabilir.");
        }

        if (amount.Minor <= 0)
        {
            return Error.Validation("transaction.amount", "Tutar sıfırdan büyük olmalıdır.");
        }

        if (kind is TransactionKind.Payment or TransactionKind.Refund && method is null)
        {
            return Error.Validation("transaction.method", "Ödeme yöntemi seçilmelidir.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
        {
            return Error.Validation("transaction.idempotency", "İşlem anahtarı geçersiz.");
        }

        return new CustomerTransaction(customerId, kind, amount.Minor, amount.Currency, Guard.Utc(nowUtc), idempotencyKey)
        {
            BranchId = branchId,
            BalanceEffectMinor = EffectOf(kind, amount.Minor),
            Method = kind is TransactionKind.Payment or TransactionKind.Refund ? method : null,
            IsDeposit = kind == TransactionKind.Payment && isDeposit,
            AppointmentId = appointmentId,
            CustomerPackageId = packageId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            RecordedByUserId = userId,
        };
    }

    public static Result<CustomerTransaction> Reverse(CustomerTransaction original, string reason, Guid? userId, DateTime nowUtc)
    {
        if (original.Kind == TransactionKind.Reversal)
        {
            return Error.Validation("transaction.reverse_reversal", "Ters kayıt tekrar ters çevrilemez.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("transaction.reverse_reason", "Düzeltme nedeni zorunludur.");
        }

        return new CustomerTransaction(original.CustomerId, TransactionKind.Reversal, original.AmountMinor, original.Currency, Guard.Utc(nowUtc), $"reverse:{original.Id}")
        {
            BranchId = original.BranchId,
            Method = original.Method,
            AppointmentId = original.AppointmentId,
            CustomerPackageId = original.CustomerPackageId,
            ReversesTransactionId = original.Id,
            BalanceEffectMinor = -original.BalanceEffectMinor,
            Description = reason.Trim(),
            RecordedByUserId = userId,
        };
    }

    public static string KindName(TransactionKind kind) => kind switch
    {
        TransactionKind.Charge => "Borçlandırma",
        TransactionKind.Payment => "Tahsilat",
        TransactionKind.Refund => "İade",
        TransactionKind.Discount => "İndirim",
        TransactionKind.Reversal => "Düzeltme (ters kayıt)",
        _ => kind.ToString(),
    };

    public static string MethodName(PaymentMethod? method) => method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.Card => "Kart",
        PaymentMethod.BankTransfer => "Havale/EFT",
        PaymentMethod.Other => "Diğer",
        PaymentMethod.GiftCard => "Hediye kartı",
        _ => "—",
    };
}

public static class AccountMath
{
    /// <summary>Positive = customer owes the business; negative = customer has credit.</summary>
    public static long Balance(IEnumerable<CustomerTransaction> entries) => entries.Sum(e => e.BalanceEffectMinor);

    /// <summary>Net cash movement by method for a set of entries (payments − refunds, reversals applied).</summary>
    public static IReadOnlyDictionary<PaymentMethod, long> CashByMethod(IEnumerable<CustomerTransaction> entries)
    {
        var result = Enum.GetValues<PaymentMethod>().ToDictionary(m => m, _ => 0L);
        foreach (var e in entries)
        {
            // Gift card payments move no money (it entered the till when the card was sold).
            if (e.Method is not { } method || method == PaymentMethod.GiftCard)
            {
                continue;
            }

            // A reversal row carries the negated balance effect of the original; cash moves the opposite way of debt.
            var cash = e.Kind switch
            {
                TransactionKind.Payment => e.AmountMinor,
                TransactionKind.Refund => -e.AmountMinor,
                TransactionKind.Reversal => -e.BalanceEffectMinor,
                _ => 0,
            };

            result[method] += cash;
        }

        return result;
    }
}
