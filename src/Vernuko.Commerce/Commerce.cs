using System.Numerics;
using System.Text.RegularExpressions;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Payments;

namespace Vernuko.Commerce;

/// <summary>
/// KDV. Prices on the sales screen include KDV (what the customer pays); purchase lines are entered without KDV, as
/// on a supplier's invoice. These are internal records: the program issues no fiscal receipt, e-Arşiv or e-Fatura.
/// </summary>
public static class Vat
{
    /// <summary>Rates in force in Turkey (genel %20, indirimli %10 ve %1; %0 for exempt goods).</summary>
    public static readonly IReadOnlyList<int> Rates = [0, 1, 10, 20];

    public static bool IsValid(int rate) => Rates.Contains(rate);

    /// <summary>The KDV inside a KDV-included amount: gross × r / (100 + r), rounded to the kuruş.</summary>
    public static long IncludedIn(long grossMinor, int rate) =>
        rate == 0 ? 0 : (long)Math.Round(grossMinor * (decimal)rate / (100 + rate), MidpointRounding.AwayFromZero);

    /// <summary>KDV on a net amount: net × r / 100, rounded to the kuruş.</summary>
    public static long On(long netMinor, int rate) =>
        (long)Math.Round(netMinor * (decimal)rate / 100, MidpointRounding.AwayFromZero);
}

/// <summary>Document numbers people read on paper and say on the phone: "S-2026-000123".</summary>
public static class DocumentNumbers
{
    public const string Sale = "S";
    public const string SaleReturn = "SI";
    public const string Purchase = "A";

    public static string Format(string prefix, int year, int sequence) => $"{prefix}-{year}-{sequence:000000}";

    /// <summary>The sequence part of a number of that prefix and year, or 0.</summary>
    public static int SequenceOf(string number, string prefix, int year)
    {
        var head = $"{prefix}-{year}-";
        return number.StartsWith(head, StringComparison.Ordinal) && int.TryParse(number.AsSpan(head.Length), out var n) ? n : 0;
    }
}

public sealed record SupplierDetails(
    string Name,
    string? TaxOffice = null,
    string? TaxNumber = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? Iban = null,
    string? Note = null);

/// <summary>A firm the business buys from. Its account (what is owed to it) is the sum of its ledger entries.</summary>
public sealed partial class Supplier : Entity
{
    private Supplier()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? TaxOffice { get; private set; }

    public string? TaxNumber { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    public string? Iban { get; private set; }

    public string? Note { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Result<Supplier> Create(SupplierDetails details)
    {
        var s = new Supplier();
        var r = s.Update(details);
        return r.IsFailure ? r.Error : s;
    }

    public Result Update(SupplierDetails d)
    {
        var name = d.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            return Error.Validation("supplier.name", "Tedarikçi adı zorunludur (en fazla 200 karakter).");
        }

        var tax = Clean(d.TaxNumber);
        if (tax is not null && !(tax.Length == 10 ? TaxNumberValidator.IsValidVkn(tax) : NationalIdValidator.IsValid(tax)))
        {
            return Error.Validation("supplier.tax_number", "Vergi numarası geçersiz: firma için 10 haneli VKN, şahıs firması için 11 haneli T.C. kimlik numarası.");
        }

        var iban = Clean(d.Iban)?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (iban is not null && !IsValidIban(iban))
        {
            return Error.Validation("supplier.iban", "IBAN geçersiz (TR ile başlayan 26 karakter).");
        }

        Name = name;
        TaxOffice = Clean(d.TaxOffice);
        TaxNumber = tax;
        Phone = Clean(d.Phone);
        Email = Clean(d.Email);
        Address = Clean(d.Address);
        Iban = iban;
        Note = Clean(d.Note);
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>Turkish IBAN: "TR" + 24 digits, ISO 13616 mod-97 check.</summary>
    public static bool IsValidIban(string iban)
    {
        if (!TrIban().IsMatch(iban))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsAsciiLetter(c) ? (c - 'A' + 10).ToString(System.Globalization.CultureInfo.InvariantCulture) : c.ToString()));
        return BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) % 97 == 1;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    [GeneratedRegex("^TR[0-9]{24}$")]
    private static partial Regex TrIban();
}

public enum SupplierEntryKind
{
    /// <summary>Goods bought: owed to the supplier.</summary>
    Purchase,

    /// <summary>Money paid to the supplier.</summary>
    Payment,

    /// <summary>Goods sent back: owed less.</summary>
    Return,

    /// <summary>Cancels an earlier entry (entries are never edited or deleted).</summary>
    Reversal,
}

/// <summary>Append-only supplier account. Positive balance: the business owes the supplier.</summary>
public sealed class SupplierTransaction
{
    private SupplierTransaction()
    {
    }

    public Guid Id { get; private set; }

    public Guid SupplierId { get; private set; }

    public SupplierEntryKind Kind { get; private set; }

    public long AmountMinor { get; private set; }

    public long BalanceEffectMinor { get; private set; }

    public PaymentMethod? Method { get; private set; }

    public Guid? PurchaseId { get; private set; }

    public Guid? ReversesId { get; private set; }

    public string? Description { get; private set; }

    public Guid? UserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public static Result<SupplierTransaction> Create(Guid supplierId, SupplierEntryKind kind, long amountMinor, PaymentMethod? method, Guid? purchaseId, string? description, Guid? userId, DateTime nowUtc, string idempotencyKey)
    {
        if (kind == SupplierEntryKind.Reversal)
        {
            return Error.Validation("supplier_tx.kind", "Ters kayıt yalnızca Reverse ile oluşturulur.");
        }

        if (amountMinor <= 0)
        {
            return Error.Validation("supplier_tx.amount", "Tutar sıfırdan büyük olmalıdır.");
        }

        if (kind == SupplierEntryKind.Payment && (method is null || method == PaymentMethod.GiftCard))
        {
            return Error.Validation("supplier_tx.method", "Ödeme yöntemi seçin: nakit, kart, havale/EFT veya diğer.");
        }

        return new SupplierTransaction
        {
            Id = Ids.New(),
            SupplierId = supplierId,
            Kind = kind,
            AmountMinor = amountMinor,
            BalanceEffectMinor = kind == SupplierEntryKind.Purchase ? amountMinor : -amountMinor,
            Method = kind == SupplierEntryKind.Payment ? method : null,
            PurchaseId = purchaseId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UserId = userId,
            OccurredAtUtc = Guard.Utc(nowUtc),
            IdempotencyKey = Guard.NotBlank(idempotencyKey),
        };
    }

    public static SupplierTransaction Reverse(SupplierTransaction original, string reason, Guid? userId, DateTime nowUtc) => new()
    {
        Id = Ids.New(),
        SupplierId = original.SupplierId,
        Kind = SupplierEntryKind.Reversal,
        AmountMinor = original.AmountMinor,
        BalanceEffectMinor = -original.BalanceEffectMinor,
        Method = original.Method,
        PurchaseId = original.PurchaseId,
        ReversesId = original.Id,
        Description = Guard.NotBlank(reason),
        UserId = userId,
        OccurredAtUtc = Guard.Utc(nowUtc),
        IdempotencyKey = $"reverse:{original.Id}",
    };

    public static string KindName(SupplierEntryKind kind) => kind switch
    {
        SupplierEntryKind.Purchase => "Alış",
        SupplierEntryKind.Payment => "Ödeme",
        SupplierEntryKind.Return => "Alış iadesi",
        _ => "Ters kayıt",
    };
}

/// <summary>A line as the sales screen or the goods receipt sends it.</summary>
/// <param name="SourceLineId">For a return line: the sale line it takes back.</param>
public sealed record LineInput(Guid ProductId, string ProductName, int Quantity, long UnitPriceMinor, int VatRate, long DiscountMinor = 0, string? Barcode = null, long UnitCostMinor = 0, Guid? SourceLineId = null);

public enum SaleKind
{
    Sale,
    Return,
}

/// <summary>
/// A counter sale or a return (satış fişi / iade fişi). Prices include KDV. Internal record only: the legal receipt
/// comes from the business's ÖKC (yazar kasa) or e-Arşiv; the screen says so.
/// </summary>
public sealed class Sale : Entity
{
    private readonly List<SaleLine> _lines = [];

    private Sale()
    {
    }

    public string Number { get; private set; } = string.Empty;

    public SaleKind Kind { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>For a return: the sale it returns goods from.</summary>
    public Guid? ReturnOfSaleId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? UserId { get; private set; }

    public IReadOnlyList<SaleLine> Lines => _lines;

    /// <summary>Σ quantity × unit price, before discounts.</summary>
    public long GrossMinor { get; private set; }

    public long LineDiscountMinor { get; private set; }

    public long DocumentDiscountMinor { get; private set; }

    /// <summary>What the customer pays (or gets back, for a return), KDV included.</summary>
    public long TotalMinor { get; private set; }

    public long VatMinor { get; private set; }

    public long CashMinor { get; private set; }

    public long CardMinor { get; private set; }

    public long TransferMinor { get; private set; }

    /// <summary>Left on the customer's account (veresiye / vadeli). For a return: credited to the account.</summary>
    public long OnAccountMinor { get; private set; }

    public string? Note { get; private set; }

    /// <summary>KVKK erasure of the customer: free text about the sale may name people.</summary>
    public void ClearNote() => Note = null;

    public static Result<Sale> Create(string number, Guid customerId, IReadOnlyList<LineInput> lines, long documentDiscountMinor, long cashMinor, long cardMinor, long transferMinor, bool allowOnAccount, string? note, Guid? userId, DateTime nowUtc) =>
        Build(SaleKind.Sale, number, customerId, null, lines, documentDiscountMinor, cashMinor, cardMinor, transferMinor, allowOnAccount, note, userId, nowUtc);

    /// <summary>
    /// Goods brought back. Lines carry the original prices (after the original discounts, apportioned by the caller).
    /// Paid back in cash/card/transfer, the rest credited to the account (only for a named customer).
    /// </summary>
    public static Result<Sale> CreateReturn(string number, Sale original, IReadOnlyList<LineInput> lines, long cashMinor, long cardMinor, long transferMinor, bool allowOnAccount, string? note, Guid? userId, DateTime nowUtc) =>
        original.Kind != SaleKind.Sale
            ? Error.Validation("sale.return_of_return", "İade fişinden iade alınamaz.")
            : Build(SaleKind.Return, number, original.CustomerId, original.Id, lines, 0, cashMinor, cardMinor, transferMinor, allowOnAccount, note, userId, nowUtc);

    /// <summary>Splits <paramref name="discount"/> (≤ the sum) over the amounts in proportion; whole kuruş, exact sum.</summary>
    internal static long[] SpreadDiscount(IReadOnlyList<long> amounts, long discount)
    {
        var shares = new long[amounts.Count];
        var total = amounts.Sum();
        if (discount <= 0 || total <= 0)
        {
            return shares;
        }

        var remainders = new decimal[amounts.Count];
        long assigned = 0;
        for (var i = 0; i < amounts.Count; i++)
        {
            var exact = (decimal)discount * amounts[i] / total;
            shares[i] = (long)decimal.Floor(exact);
            remainders[i] = exact - shares[i];
            assigned += shares[i];
        }

        foreach (var i in Enumerable.Range(0, amounts.Count).OrderByDescending(i => remainders[i]).ThenByDescending(i => amounts[i]))
        {
            if (assigned == discount)
            {
                break;
            }

            if (shares[i] < amounts[i])
            {
                shares[i]++;
                assigned++;
            }
        }

        return shares;
    }

    private static Result<Sale> Build(SaleKind kind, string number, Guid customerId, Guid? returnOf, IReadOnlyList<LineInput> lines, long documentDiscountMinor, long cashMinor, long cardMinor, long transferMinor, bool allowOnAccount, string? note, Guid? userId, DateTime nowUtc)
    {
        if (lines.Count == 0)
        {
            return Error.Validation("sale.empty", kind == SaleKind.Sale ? "Satışta en az bir ürün olmalı." : "İade edilecek en az bir ürün seçin.");
        }

        if (lines.Count > 500)
        {
            return Error.Validation("sale.too_many", "Bir fişte en fazla 500 satır olabilir.");
        }

        var sale = new Sale
        {
            Number = Guard.NotBlank(number),
            Kind = kind,
            CustomerId = customerId,
            ReturnOfSaleId = returnOf,
            OccurredAtUtc = Guard.Utc(nowUtc),
            UserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };

        var lineNo = 0;
        foreach (var l in lines)
        {
            if (l.Quantity is < 1 or > 100_000)
            {
                return Error.Validation("sale.quantity", $"{l.ProductName}: miktar 1 ile 100.000 arasında olmalı.");
            }

            if (l.UnitPriceMinor < 0)
            {
                return Error.Validation("sale.price", $"{l.ProductName}: fiyat negatif olamaz.");
            }

            if (!Vat.IsValid(l.VatRate))
            {
                return Error.Validation("sale.vat", $"{l.ProductName}: KDV oranı %0, %1, %10 veya %20 olmalı.");
            }

            var gross = checked(l.UnitPriceMinor * l.Quantity);
            if (l.DiscountMinor < 0 || l.DiscountMinor > gross)
            {
                return Error.Validation("sale.line_discount", $"{l.ProductName}: indirim satır tutarını geçemez.");
            }

            sale._lines.Add(new SaleLine(sale.Id, ++lineNo, l, gross - l.DiscountMinor));
            sale.GrossMinor = checked(sale.GrossMinor + gross);
            sale.LineDiscountMinor += l.DiscountMinor;
        }

        var afterLines = sale.GrossMinor - sale.LineDiscountMinor;
        if (documentDiscountMinor < 0 || documentDiscountMinor > afterLines)
        {
            return Error.Validation("sale.discount", "Fiş indirimi toplamı geçemez.");
        }

        sale.DocumentDiscountMinor = documentDiscountMinor;
        sale.TotalMinor = afterLines - documentDiscountMinor;

        // KDV per line on its share of the total. The document discount is spread by line amount (largest remainder):
        // the shares add up exactly, a free line gets nothing and no share is larger than its line.
        var shares = SpreadDiscount(sale._lines.Select(l => l.TotalMinor).ToList(), documentDiscountMinor);
        long vat = 0;
        for (var i = 0; i < sale._lines.Count; i++)
        {
            var line = sale._lines[i];
            line.ApplyDocumentDiscount(shares[i]);
            vat += Vat.IncludedIn(line.NetTotalMinor, line.VatRate);
        }

        sale.VatMinor = vat;

        if (cashMinor < 0 || cardMinor < 0 || transferMinor < 0)
        {
            return Error.Validation("sale.payment", "Ödeme tutarı negatif olamaz.");
        }

        var paid = checked(cashMinor + cardMinor + transferMinor);
        if (paid > sale.TotalMinor)
        {
            return Error.Validation("sale.overpaid", kind == SaleKind.Sale
                ? "Ödeme toplamı fiş tutarını geçiyor. Para üstünü nakitten düşün."
                : "Geri ödeme, iade tutarını geçemez.");
        }

        var onAccount = sale.TotalMinor - paid;
        if (onAccount > 0 && !allowOnAccount)
        {
            return Error.Validation("sale.on_account", kind == SaleKind.Sale
                ? "Ödemenin tamamı alınmadı. Veresiye için kayıtlı bir müşteri seçin."
                : "İade tutarının tamamını geri ödeyin; hesaba alacak yazmak için kayıtlı bir müşteri gerekir.");
        }

        sale.CashMinor = cashMinor;
        sale.CardMinor = cardMinor;
        sale.TransferMinor = transferMinor;
        sale.OnAccountMinor = onAccount;
        return sale;
    }
}

public sealed class SaleLine
{
    private SaleLine()
    {
    }

    internal SaleLine(Guid saleId, int lineNo, LineInput input, long totalMinor)
    {
        Id = Ids.New();
        SaleId = saleId;
        LineNo = lineNo;
        ProductId = input.ProductId;
        ProductName = Guard.NotBlank(input.ProductName);
        Barcode = input.Barcode;
        Quantity = input.Quantity;
        UnitPriceMinor = input.UnitPriceMinor;
        VatRate = input.VatRate;
        DiscountMinor = input.DiscountMinor;
        TotalMinor = totalMinor;
        NetTotalMinor = totalMinor;
        UnitCostMinor = input.UnitCostMinor;
        ReturnOfLineId = input.SourceLineId;
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    public int LineNo { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public string? Barcode { get; private set; }

    public int Quantity { get; private set; }

    public long UnitPriceMinor { get; private set; }

    public int VatRate { get; private set; }

    public long DiscountMinor { get; private set; }

    /// <summary>Quantity × price − line discount.</summary>
    public long TotalMinor { get; private set; }

    /// <summary>After this line's share of the document discount: what the line really brought in.</summary>
    public long NetTotalMinor { get; private set; }

    /// <summary>Cost at the time of sale (for the profit report).</summary>
    public long UnitCostMinor { get; private set; }

    /// <summary>On a return: the sale line taken back.</summary>
    public Guid? ReturnOfLineId { get; private set; }

    internal void ApplyDocumentDiscount(long share) => NetTotalMinor = TotalMinor - share;
}

/// <summary>Goods received from a supplier (mal kabul / alış). Unit costs without KDV, as on the supplier's invoice.</summary>
public sealed class Purchase : Entity
{
    private readonly List<PurchaseLine> _lines = [];

    private Purchase()
    {
    }

    public string Number { get; private set; } = string.Empty;

    public Guid SupplierId { get; private set; }

    /// <summary>The supplier's invoice or irsaliye number.</summary>
    public string? DocumentNo { get; private set; }

    public DateOnly DocumentDate { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Note { get; private set; }

    public IReadOnlyList<PurchaseLine> Lines => _lines;

    public long NetMinor { get; private set; }

    public long VatMinor { get; private set; }

    public long TotalMinor { get; private set; }

    public static Result<Purchase> Create(string number, Guid supplierId, string? documentNo, DateOnly documentDate, IReadOnlyList<LineInput> lines, string? note, Guid? userId, DateTime nowUtc)
    {
        if (lines.Count == 0)
        {
            return Error.Validation("purchase.empty", "Mal kabulde en az bir ürün olmalı.");
        }

        if (lines.Count > 500)
        {
            return Error.Validation("purchase.too_many", "Bir belgede en fazla 500 satır olabilir.");
        }

        var p = new Purchase
        {
            Number = Guard.NotBlank(number),
            SupplierId = supplierId,
            DocumentNo = string.IsNullOrWhiteSpace(documentNo) ? null : documentNo.Trim(),
            DocumentDate = documentDate,
            OccurredAtUtc = Guard.Utc(nowUtc),
            UserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        var lineNo = 0;
        foreach (var l in lines)
        {
            if (l.Quantity is < 1 or > 1_000_000)
            {
                return Error.Validation("purchase.quantity", $"{l.ProductName}: miktar 1 ile 1.000.000 arasında olmalı.");
            }

            if (l.UnitPriceMinor < 0)
            {
                return Error.Validation("purchase.cost", $"{l.ProductName}: maliyet negatif olamaz.");
            }

            if (!Vat.IsValid(l.VatRate))
            {
                return Error.Validation("purchase.vat", $"{l.ProductName}: KDV oranı %0, %1, %10 veya %20 olmalı.");
            }

            var net = checked(l.UnitPriceMinor * l.Quantity) - l.DiscountMinor;
            if (l.DiscountMinor < 0 || net < 0)
            {
                return Error.Validation("purchase.discount", $"{l.ProductName}: indirim satır tutarını geçemez.");
            }

            var line = new PurchaseLine(p.Id, ++lineNo, l, net, Vat.On(net, l.VatRate));
            p._lines.Add(line);
            p.NetMinor += line.NetMinor;
            p.VatMinor += line.VatMinor;
        }

        p.TotalMinor = p.NetMinor + p.VatMinor;
        return p;
    }
}

public sealed class PurchaseLine
{
    private PurchaseLine()
    {
    }

    internal PurchaseLine(Guid purchaseId, int lineNo, LineInput input, long netMinor, long vatMinor)
    {
        Id = Ids.New();
        PurchaseId = purchaseId;
        LineNo = lineNo;
        ProductId = input.ProductId;
        ProductName = Guard.NotBlank(input.ProductName);
        Quantity = input.Quantity;
        UnitCostMinor = input.UnitPriceMinor;
        VatRate = input.VatRate;
        DiscountMinor = input.DiscountMinor;
        NetMinor = netMinor;
        VatMinor = vatMinor;
    }

    public Guid Id { get; private set; }

    public Guid PurchaseId { get; private set; }

    public int LineNo { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>Without KDV.</summary>
    public long UnitCostMinor { get; private set; }

    public int VatRate { get; private set; }

    public long DiscountMinor { get; private set; }

    public long NetMinor { get; private set; }

    public long VatMinor { get; private set; }

    /// <summary>Cost per unit after the line discount (what the stock is worth), without KDV.</summary>
    public long EffectiveUnitCostMinor => Quantity == 0 ? 0 : NetMinor / Quantity;
}
