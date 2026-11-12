using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Payments;

namespace Vernuko.Application;

// ---------- Commissions ----------
public sealed record CommissionRuleDto(
    Guid Id,
    CommissionBasis Basis,
    Guid? StaffMemberId,
    string StaffName,
    Guid? ItemId,
    string ItemName,
    CommissionRateKind RateKind,
    int PercentBasisPoints,
    long FixedMinor,
    bool IsActive)
{
    public string BasisName => CommissionRule.BasisName(Basis);

    public string RateText => RateKind == CommissionRateKind.Percent ? $"%{PercentBasisPoints / 100m:0.##}" : new Money(FixedMinor).ToDisplayString();
}

public sealed record StaffCommissionTotal(Guid StaffMemberId, string StaffName, long ServiceMinor, long PackageSaleMinor, long ProductSaleMinor)
{
    public long TotalMinor => ServiceMinor + PackageSaleMinor + ProductSaleMinor;
}

public sealed record CommissionLine(DateTime OccurredAtUtc, string StaffName, CommissionBasis Basis, string? Description, long BaseAmountMinor, long AmountMinor)
{
    public string BasisName => CommissionRule.BasisName(Basis);
}

public sealed record CommissionReport(DateOnly From, DateOnly To, IReadOnlyList<StaffCommissionTotal> Totals, IReadOnlyList<CommissionLine> Lines)
{
    public long GrandTotalMinor => Totals.Sum(t => t.TotalMinor);
}

// ---------- Inventory ----------
public sealed record ProductDto(Guid Id, string Name, string? Sku, string Unit, long SalePriceMinor, long CostMinor, int MinimumStock, bool IsActive, int Stock)
{
    public bool IsLow => IsActive && Stock <= MinimumStock;

    public string StockText => $"{Stock} {Unit}";
}

public sealed record StockMovementDto(DateTime AtUtc, StockMovementKind Kind, int QuantityDelta, string? Note, string? CustomerName, string? UserName)
{
    public string KindName => StockMovement.KindName(Kind);
}

public sealed record ServiceMaterialDto(Guid ServiceId, Guid ProductId, string ProductName, string Unit, int Quantity);

public sealed record SellProductRequest(
    Guid CustomerId,
    Guid ProductId,
    int Quantity,
    long UnitPriceMinor,
    Guid? SoldByStaffId,
    PaymentMethod? PayNowMethod,
    string IdempotencyKey);

// ---------- Consents & files ----------
public sealed record ConsentTemplateDto(Guid Id, string Title, string Body, int TextVersion, bool IsActive);

public sealed record CustomerConsentDto(
    Guid Id,
    string Title,
    int TemplateVersion,
    ConsentMethod Method,
    DateTime AcceptedAtUtc,
    string? RecordedBy,
    string? Note,
    DateTime? RevokedAtUtc,
    string? RevokeReason)
{
    public string MethodName => CustomerConsent.MethodName(Method);

    public bool IsActive => RevokedAtUtc is null;

    public string StatusText => IsActive ? "Geçerli" : "Geri alındı";
}

public sealed record CustomerFileDto(Guid Id, CustomerFileKind Kind, string OriginalName, long SizeBytes, DateTime CreatedAtUtc, string? Note, bool IsImage)
{
    public string KindName => CustomerFile.KindName(Kind);

    public string SizeText => SizeBytes < 1024 * 1024 ? $"{SizeBytes / 1024.0:0} KB" : $"{SizeBytes / 1024.0 / 1024.0:0.0} MB";
}
