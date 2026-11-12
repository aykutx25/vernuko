using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Vernuko.Persistence;

namespace Vernuko.Application.Common;

/// <summary>Typed access to the AppSettings key/value table.</summary>
public static class SettingsStore
{
    public static async Task<string?> GetAsync(VernukoDbContext db, string key, CancellationToken ct = default) =>
        await db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    public static async Task SetAsync(VernukoDbContext db, string key, string value, DateTime nowUtc, CancellationToken ct = default)
    {
        var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            db.AppSettings.Add(new AppSetting(key, value, nowUtc));
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = nowUtc;
        }
    }

    public static async Task RemoveAsync(VernukoDbContext db, string key, CancellationToken ct = default)
    {
        var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is not null)
        {
            db.AppSettings.Remove(existing);
        }
    }

    public static async Task<DateTime?> GetUtcAsync(VernukoDbContext db, string key, CancellationToken ct = default)
    {
        var raw = await GetAsync(db, key, ct);
        return raw is not null && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : null;
    }

    public static Task SetUtcAsync(VernukoDbContext db, string key, DateTime valueUtc, DateTime nowUtc, CancellationToken ct = default) =>
        SetAsync(db, key, valueUtc.ToString("O", CultureInfo.InvariantCulture), nowUtc, ct);
}
