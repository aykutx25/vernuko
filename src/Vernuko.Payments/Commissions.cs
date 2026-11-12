using Vernuko.Core;

namespace Vernuko.Payments;

public enum CommissionBasis
{
    /// <summary>The staff member who performed the service (seans uygulayan).</summary>
    ServiceDelivered,

    /// <summary>The staff member who sold a package (paketi satan).</summary>
    PackageSold,

    /// <summary>The staff member who sold a product.</summary>
    ProductSold,
}

public enum CommissionRateKind
{
    Percent,
    FixedAmount,
}

/// <summary>
/// Commission rule. Null staff/item means "everyone"/"every item". The most specific matching rule wins:
/// staff + item &gt; staff &gt; item &gt; general.
/// </summary>
public sealed class CommissionRule : Entity
{
    private CommissionRule()
    {
    }

    public CommissionRule(CommissionBasis basis, Guid? staffMemberId, Guid? itemId, CommissionRateKind rateKind, int percentBasisPoints, long fixedMinor)
    {
        var r = Update(basis, staffMemberId, itemId, rateKind, percentBasisPoints, fixedMinor);
        if (r.IsFailure)
        {
            throw new ArgumentException(r.Error.Message);
        }

        IsActive = true;
    }

    public CommissionBasis Basis { get; private set; }

    public Guid? StaffMemberId { get; private set; }

    /// <summary>Service id (ServiceDelivered / PackageSold) or product id (ProductSold).</summary>
    public Guid? ItemId { get; private set; }

    public CommissionRateKind RateKind { get; private set; }

    /// <summary>Percent in basis points: 1000 = %10.</summary>
    public int PercentBasisPoints { get; private set; }

    public long FixedMinor { get; private set; }

    public bool IsActive { get; private set; }

    public int Specificity => (StaffMemberId is null ? 0 : 2) + (ItemId is null ? 0 : 1);

    public static Result Validate(CommissionRateKind kind, int percentBasisPoints, long fixedMinor) =>
        kind == CommissionRateKind.Percent && percentBasisPoints is < 0 or > 10_000
            ? Error.Validation("commission.percent", "Prim oranı %0 ile %100 arasında olmalıdır.")
            : kind == CommissionRateKind.FixedAmount && fixedMinor < 0
                ? Error.Validation("commission.fixed", "Sabit prim negatif olamaz.")
                : Result.Success();

    public Result Update(CommissionBasis basis, Guid? staffMemberId, Guid? itemId, CommissionRateKind rateKind, int percentBasisPoints, long fixedMinor)
    {
        var v = Validate(rateKind, percentBasisPoints, fixedMinor);
        if (v.IsFailure)
        {
            return v;
        }

        Basis = basis;
        StaffMemberId = staffMemberId;
        ItemId = itemId;
        RateKind = rateKind;
        PercentBasisPoints = rateKind == CommissionRateKind.Percent ? percentBasisPoints : 0;
        FixedMinor = rateKind == CommissionRateKind.FixedAmount ? fixedMinor : 0;
        return Result.Success();
    }

    public void SetActive(bool active) => IsActive = active;

    public long Calculate(long baseAmountMinor, int quantity = 1) => RateKind switch
    {
        CommissionRateKind.Percent => (long)Math.Round(baseAmountMinor * (PercentBasisPoints / 10_000m), MidpointRounding.AwayFromZero),
        _ => FixedMinor * Math.Max(1, quantity),
    };

    public string Describe() => RateKind == CommissionRateKind.Percent
        ? $"%{PercentBasisPoints / 100m:0.##}"
        : new Money(FixedMinor).ToDisplayString();

    public static string BasisName(CommissionBasis basis) => basis switch
    {
        CommissionBasis.ServiceDelivered => "Hizmet / seans uygulama",
        CommissionBasis.PackageSold => "Paket satışı",
        _ => "Ürün satışı",
    };
}

public static class CommissionRules
{
    public static CommissionRule? Resolve(IEnumerable<CommissionRule> rules, CommissionBasis basis, Guid staffMemberId, Guid itemId) =>
        rules
            .Where(r => r.IsActive && r.Basis == basis
                        && (r.StaffMemberId is null || r.StaffMemberId == staffMemberId)
                        && (r.ItemId is null || r.ItemId == itemId))
            .OrderByDescending(r => r.Specificity)
            .ThenByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();
}

/// <summary>Append-only commission ledger. Corrections are reversal rows.</summary>
public sealed class CommissionEntry
{
    private CommissionEntry()
    {
    }

    private CommissionEntry(Guid staffMemberId, CommissionBasis basis, Guid sourceId, long baseAmountMinor, long amountMinor, DateTime atUtc, string idempotencyKey)
    {
        Id = Ids.New();
        StaffMemberId = staffMemberId;
        Basis = basis;
        SourceId = sourceId;
        BaseAmountMinor = baseAmountMinor;
        AmountMinor = amountMinor;
        OccurredAtUtc = atUtc;
        IdempotencyKey = idempotencyKey;
    }

    public Guid Id { get; private set; }

    public Guid StaffMemberId { get; private set; }

    public CommissionBasis Basis { get; private set; }

    /// <summary>Appointment, customer package or customer transaction (product sale) the commission comes from.</summary>
    public Guid SourceId { get; private set; }

    public long BaseAmountMinor { get; private set; }

    /// <summary>Signed: reversal rows are negative.</summary>
    public long AmountMinor { get; private set; }

    public Guid? RuleId { get; private set; }

    public Guid? ReversesEntryId { get; private set; }

    public string? Description { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public static CommissionEntry Create(Guid staffMemberId, CommissionBasis basis, Guid sourceId, long baseAmountMinor, CommissionRule rule, int quantity, string? description, DateTime nowUtc) =>
        new(staffMemberId, basis, sourceId, baseAmountMinor, rule.Calculate(baseAmountMinor, quantity), Guard.Utc(nowUtc), $"commission:{basis}:{sourceId:N}:{Ids.New():N}")
        {
            RuleId = rule.Id,
            Description = description,
        };

    public static CommissionEntry Reverse(CommissionEntry original, string reason, DateTime nowUtc) =>
        new(original.StaffMemberId, original.Basis, original.SourceId, -original.BaseAmountMinor, -original.AmountMinor, Guard.Utc(nowUtc), $"commission-reversal:{original.Id:N}")
        {
            RuleId = original.RuleId,
            ReversesEntryId = original.Id,
            Description = reason,
        };
}
