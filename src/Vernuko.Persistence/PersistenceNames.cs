namespace Vernuko.Persistence;

/// <summary>Names of shadow properties (database-only columns the domain model does not need to know about).</summary>
public static class PersistenceNames
{
    /// <summary>The idempotency key of the sale that created a package or a stock-out (null for other rows).</summary>
    public const string SaleKey = "SaleKey";
}
