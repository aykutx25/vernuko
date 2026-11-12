using Microsoft.EntityFrameworkCore;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Persistence;

namespace Vernuko.Application.Common;

public static class BusinessContext
{
    public static async Task<(BusinessSettings Settings, TimeZoneInfo Zone)> LoadAsync(VernukoDbContext db, CancellationToken ct)
    {
        var settings = await db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Business settings are missing; setup has not been completed.");
        return (settings, TimeZones.Find(settings.TimeZoneId));
    }
}
