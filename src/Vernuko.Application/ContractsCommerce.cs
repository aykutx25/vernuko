using Vernuko.Commerce;
using Vernuko.Payments;

namespace Vernuko.Application;

// ---------- Stok mode: suppliers, sales, goods receipts, stock counts ----------

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? TaxOffice,
    string? TaxNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Iban,
    string? Note,
    bool IsActive,
    long BalanceMinor)
{
    /// <summary>Positive: owed to the supplier.</summary>
    public bool OwesSupplier => BalanceMinor > 0;
}

public sealed record SupplierEntryDto(DateTime AtUtc, SupplierEntryKind Kind, long AmountMinor, long BalanceEffectMinor, PaymentMethod? Method, string? Description, string? DocumentNumber, long RunningBalanceMinor);

public sealed record SupplierStatementDto(SupplierDto Supplier, IReadOnlyList<SupplierEntryDto> Entries);

/// <summary>A line on the sales screen: price includes KDV.</summary>
public sealed record SaleLineRequest(Guid ProductId, int Quantity, long UnitPriceMinor, long DiscountMinor = 0);

/// <summary>
/// CustomerId null: a walk-in sale (kayıtsız müşteri), paid in full. With a customer, what is not paid stays on
/// their account (veresiye / vadeli).
/// </summary>
public sealed record CreateSaleRequest(
    Guid? CustomerId,
    IReadOnlyList<SaleLineRequest> Lines,
    long DocumentDiscountMinor,
    long CashMinor,
    long CardMinor,
    long TransferMinor,
    string? Note,
    string IdempotencyKey);

public sealed record ReturnLineRequest(Guid SaleLineId, int Quantity);

public sealed record CreateReturnRequest(Guid SaleId, IReadOnlyList<ReturnLineRequest> Lines, long CashMinor, long CardMinor, long TransferMinor, string? Note, string IdempotencyKey);

public sealed record SaleLineDto(Guid Id, int LineNo, Guid ProductId, string ProductName, string? Barcode, int Quantity, long UnitPriceMinor, int VatRate, long DiscountMinor, long TotalMinor, long NetTotalMinor, int ReturnedQuantity)
{
    public int ReturnableQuantity => Quantity - ReturnedQuantity;
}

public sealed record SaleDto(
    Guid Id,
    string Number,
    SaleKind Kind,
    DateTime AtUtc,
    Guid CustomerId,
    string CustomerName,
    bool IsWalkIn,
    long GrossMinor,
    long DiscountMinor,
    long TotalMinor,
    long VatMinor,
    long CashMinor,
    long CardMinor,
    long TransferMinor,
    long OnAccountMinor,
    string? Note,
    string? UserName,
    IReadOnlyList<SaleLineDto> Lines,
    string? ReturnOfNumber);

public sealed record SaleSummaryDto(Guid Id, string Number, SaleKind Kind, DateTime AtUtc, string CustomerName, int ItemCount, long TotalMinor, long OnAccountMinor);

/// <summary>Unit cost without KDV, as on the supplier's invoice.</summary>
public sealed record PurchaseLineRequest(Guid ProductId, int Quantity, long UnitCostMinor, int VatRate, long DiscountMinor = 0);

public sealed record CreatePurchaseRequest(
    Guid SupplierId,
    string? DocumentNo,
    DateOnly DocumentDate,
    IReadOnlyList<PurchaseLineRequest> Lines,
    bool UpdateProductCosts,
    long PayNowMinor,
    PaymentMethod? PayNowMethod,
    string? Note,
    string IdempotencyKey);

public sealed record PurchaseSummaryDto(Guid Id, string Number, DateTime AtUtc, string SupplierName, string? DocumentNo, DateOnly DocumentDate, int ItemCount, long NetMinor, long VatMinor, long TotalMinor);

public sealed record PurchaseLineDto(int LineNo, Guid ProductId, string ProductName, int Quantity, long UnitCostMinor, int VatRate, long DiscountMinor, long NetMinor, long VatMinor);

public sealed record PurchaseDto(PurchaseSummaryDto Summary, Guid SupplierId, string? Note, IReadOnlyList<PurchaseLineDto> Lines);

public sealed record SupplierPaymentRequest(Guid SupplierId, long AmountMinor, PaymentMethod Method, string? Note, string IdempotencyKey);

public sealed record StockCountLine(Guid ProductId, int CountedQuantity);

public sealed record StockCountRequest(IReadOnlyList<StockCountLine> Lines, string? Note, string IdempotencyKey);

public sealed record StockCountResult(int CountedProducts, int AdjustedProducts, int QuantityDifference, long ValueDifferenceMinor);

public sealed record VatBreakdownDto(int Rate, long GrossMinor, long VatMinor);

/// <summary>Gün sonu: what was sold and how it was paid on one business day.</summary>
public sealed record DayEndDto(
    DateOnly Day,
    int SaleCount,
    int ReturnCount,
    long SalesMinor,
    long ReturnsMinor,
    long CashMinor,
    long CardMinor,
    long TransferMinor,
    long OnAccountMinor,
    long CostMinor,
    IReadOnlyList<VatBreakdownDto> Vat)
{
    public long NetSalesMinor => SalesMinor - ReturnsMinor;

    /// <summary>Gross profit estimate: net sales without KDV − cost of goods sold (at the cost known at sale time).</summary>
    public long GrossProfitMinor => NetSalesMinor - Vat.Sum(v => v.VatMinor) - CostMinor;
}

public sealed record ReorderLineDto(Guid ProductId, string ProductName, string? Barcode, string Unit, int Stock, int MinimumStock, int SuggestedQuantity, long LastCostMinor);

public sealed record ReorderGroupDto(Guid? SupplierId, string SupplierName, IReadOnlyList<ReorderLineDto> Lines);

public sealed record StockValueDto(int ProductCount, int UnitCount, long CostValueMinor, long SaleValueMinor, int NegativeStockProducts);
