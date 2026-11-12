using Microsoft.EntityFrameworkCore;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Common;

/// <summary>Appends hash-chained audit entries inside the caller's transaction.</summary>
public sealed class AuditWriter(UserSession session, TimeProvider time)
{
    public Task AppendAsync(VernukoDbContext db, string action, string entityType, Guid? entityId, string summary, CancellationToken ct = default)
    {
        var userId = session.UserId == Guid.Empty ? null : session.UserId;
        return AppendAsAsync(db, userId, session.Username, action, entityType, entityId, summary, ct);
    }

    /// <summary>Appends with an explicit actor (login attempts, setup) without touching the session.</summary>
    public async Task AppendAsAsync(VernukoDbContext db, Guid? actorId, string? actorName, string action, string entityType, Guid? entityId, string summary, CancellationToken ct = default)
    {
        var pending = db.ChangeTracker.Entries<AuditEntry>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefault();

        long sequence;
        string previous;
        if (pending is not null)
        {
            sequence = pending.Sequence + 1;
            previous = pending.Hash;
        }
        else
        {
            var last = await db.AuditEntries.AsNoTracking()
                .OrderByDescending(a => a.Sequence)
                .Select(a => new { a.Sequence, a.Hash })
                .FirstOrDefaultAsync(ct);
            sequence = (last?.Sequence ?? 0) + 1;
            previous = last?.Hash ?? HashChain.Genesis;
        }

        db.AuditEntries.Add(new AuditEntry(sequence, time.GetUtcNow().UtcDateTime, actorId, actorName, action, entityType, entityId, summary, previous));
    }
}
