namespace Vernuko.Core;

/// <summary>Base type for persisted aggregates.</summary>
public abstract class Entity
{
    protected Entity()
    {
        Id = Ids.New();
    }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Ids.New() : id;
    }

    public Guid Id { get; protected set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Optimistic concurrency token, incremented on every update by the persistence layer.</summary>
    public long Version { get; set; }
}
