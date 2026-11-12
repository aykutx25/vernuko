using System.Globalization;

namespace Vernuko.Backups;

public sealed record RetentionPolicy(int Daily = 7, int Weekly = 4, int Monthly = 3)
{
    public static readonly RetentionPolicy Default = new();
}

public sealed record BackupRecord(string Name, DateTime CreatedAtUtc, BackupTrigger Trigger);

/// <summary>
/// Grandfather-father-son retention: keep the newest backup of each of the last N days, N weeks and N months.
/// The newest backup is always kept. Safety backups (pre-migration / pre-restore / pre-update) are managed separately
/// and never deleted by this policy.
/// </summary>
public static class BackupRetention
{
    public static IReadOnlyList<BackupRecord> SelectForDeletion(IReadOnlyList<BackupRecord> backups, RetentionPolicy policy, TimeZoneInfo zone)
    {
        var regular = backups
            .Where(b => b.Trigger is BackupTrigger.Manual or BackupTrigger.Scheduled or BackupTrigger.OnExit)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();
        if (regular.Count == 0)
        {
            return [];
        }

        var keep = new HashSet<string>(StringComparer.Ordinal) { regular[0].Name };
        DateTime Local(BackupRecord b) => TimeZoneInfo.ConvertTimeFromUtc(b.CreatedAtUtc, zone);

        KeepNewestPerBucket(regular, b => Local(b).Date, policy.Daily, keep);
        KeepNewestPerBucket(regular, b => (ISOWeek.GetYear(Local(b)), ISOWeek.GetWeekOfYear(Local(b))), policy.Weekly, keep);
        KeepNewestPerBucket(regular, b => (Local(b).Year, Local(b).Month), policy.Monthly, keep);

        return regular.Where(b => !keep.Contains(b.Name)).ToList();
    }

    private static void KeepNewestPerBucket<TKey>(List<BackupRecord> newestFirst, Func<BackupRecord, TKey> bucket, int count, HashSet<string> keep)
        where TKey : notnull
    {
        if (count <= 0)
        {
            return;
        }

        var seen = new HashSet<TKey>();
        foreach (var b in newestFirst)
        {
            if (seen.Count >= count && !seen.Contains(bucket(b)))
            {
                break;
            }

            if (seen.Add(bucket(b)))
            {
                keep.Add(b.Name);
            }
        }
    }
}
