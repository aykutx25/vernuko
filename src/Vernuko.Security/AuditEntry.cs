using System.Globalization;
using Vernuko.Core;

namespace Vernuko.Security;

/// <summary>
/// Append-only, hash-chained audit record. Summaries must not contain personal data beyond what is needed
/// (prefer IDs and numbers; never message bodies, notes or national IDs).
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public AuditEntry(long sequence, DateTime occurredAtUtc, Guid? userId, string? userName, string action, string entityType, Guid? entityId, string summary, string previousHash)
    {
        Id = Ids.New();
        Sequence = sequence;
        OccurredAtUtc = Guard.Utc(occurredAtUtc);
        UserId = userId;
        UserName = userName;
        Action = Guard.NotBlank(action);
        EntityType = Guard.NotBlank(entityType);
        EntityId = entityId;
        Summary = summary ?? string.Empty;
        PreviousHash = previousHash;
        Hash = HashChain.Compute(previousHash, CanonicalContent());
    }

    public Guid Id { get; private set; }

    public long Sequence { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? UserId { get; private set; }

    public string? UserName { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string PreviousHash { get; private set; } = string.Empty;

    public string Hash { get; private set; } = string.Empty;

    public string CanonicalContent() => string.Join(
        '|',
        Sequence.ToString(CultureInfo.InvariantCulture),
        OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
        UserId?.ToString("N") ?? "-",
        UserName ?? "-",
        Action,
        EntityType,
        EntityId?.ToString("N") ?? "-",
        Summary);

    public bool IsIntact(string expectedPreviousHash) =>
        PreviousHash == expectedPreviousHash && Hash == HashChain.Compute(PreviousHash, CanonicalContent());
}
