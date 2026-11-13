using System.Security.Cryptography;
using Vernuko.Core;

namespace Vernuko.Payments;

public enum GiftCardEntryKind
{
    /// <summary>Card sold: balance loaded.</summary>
    Sale,

    /// <summary>Used to pay for a service/product.</summary>
    Redeem,

    /// <summary>A redemption was reversed: amount back on the card.</summary>
    Restore,

    /// <summary>Card cancelled; the remaining balance is removed.</summary>
    Void,
}

/// <summary>
/// Hediye kartı (SPA, güzellik): sold once (money enters the till then), used later as a payment method (no new
/// money — only the card balance goes down). The balance is the sum of an append-only ledger.
/// </summary>
public sealed class GiftCard : Entity
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    private GiftCard()
    {
    }

    /// <summary>"HK-XXXX-XXXX" (40 bits), printed on the card / sent to the recipient.</summary>
    public string Code { get; private set; } = string.Empty;

    public long InitialMinor { get; private set; }

    public string Currency { get; private set; } = "TRY";

    public Guid BuyerCustomerId { get; private set; }

    public string? RecipientName { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public DateTime? VoidedAtUtc { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public static string NewCode()
    {
        Span<char> c = stackalloc char[8];
        for (var i = 0; i < c.Length; i++)
        {
            c[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"HK-{new string(c[..4])}-{new string(c[4..])}";
    }

    public static string NormalizeCode(string? code)
    {
        var s = new string((code ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (s.StartsWith("HK", StringComparison.Ordinal) && s.Length == 10)
        {
            s = s[2..];
        }

        return s.Length == 8 ? $"HK-{s[..4]}-{s[4..]}" : string.Empty;
    }

    public static Result<GiftCard> Create(long amountMinor, Guid buyerCustomerId, string? recipientName, int? validDays, DateTime nowUtc, string idempotencyKey)
    {
        if (amountMinor <= 0 || amountMinor > 100_000_00)
        {
            return Error.Validation("giftcard.amount", "Hediye kartı tutarı 0'dan büyük ve en fazla 100.000 TL olmalı.");
        }

        if (validDays is < 1 or > 3650)
        {
            return Error.Validation("giftcard.validity", "Geçerlilik 1 gün ile 10 yıl arasında olmalı (boş = süresiz).");
        }

        return new GiftCard
        {
            Code = NewCode(),
            InitialMinor = amountMinor,
            BuyerCustomerId = buyerCustomerId,
            RecipientName = string.IsNullOrWhiteSpace(recipientName) ? null : recipientName.Trim(),
            ExpiresAtUtc = validDays is { } d ? Guard.Utc(nowUtc).AddDays(d) : null,
            CreatedAtUtc = Guard.Utc(nowUtc),
            IdempotencyKey = idempotencyKey,
        };
    }

    public Result CanRedeem(long amountMinor, long balanceMinor, DateTime nowUtc)
    {
        if (VoidedAtUtc is not null)
        {
            return Error.Validation("giftcard.voided", "Bu hediye kartı iptal edilmiş.");
        }

        if (ExpiresAtUtc is { } exp && nowUtc >= exp)
        {
            return Error.Validation("giftcard.expired", $"Hediye kartının süresi {exp:dd.MM.yyyy} tarihinde doldu.");
        }

        if (amountMinor <= 0)
        {
            return Error.Validation("giftcard.amount", "Tutar sıfırdan büyük olmalıdır.");
        }

        if (amountMinor > balanceMinor)
        {
            return Error.Validation("giftcard.balance", $"Kartta yeterli bakiye yok (kalan {balanceMinor / 100m:N2} TL).");
        }

        return Result.Success();
    }

    public void MarkVoided(DateTime nowUtc) => VoidedAtUtc = Guard.Utc(nowUtc);
}

/// <summary>One movement on a gift card (append-only; database triggers refuse UPDATE/DELETE).</summary>
public sealed class GiftCardEntry : Entity
{
    private GiftCardEntry()
    {
    }

    public Guid GiftCardId { get; private set; }

    public GiftCardEntryKind Kind { get; private set; }

    /// <summary>+ loads the card, − spends it.</summary>
    public long AmountMinor { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? TransactionId { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Note { get; private set; }

    public static GiftCardEntry Of(Guid cardId, GiftCardEntryKind kind, long signedAmountMinor, Guid? customerId, Guid? transactionId, Guid? userId, DateTime nowUtc, string? note = null) => new()
    {
        GiftCardId = cardId,
        Kind = kind,
        AmountMinor = signedAmountMinor,
        CustomerId = customerId,
        TransactionId = transactionId,
        UserId = userId,
        CreatedAtUtc = Guard.Utc(nowUtc),
        Note = note,
    };
}
