namespace Vernuko.Core;

/// <summary>
/// Conversion between a business' local wall-clock time and UTC. Appointments are stored as UTC instants;
/// the business time zone (IANA id, e.g. "Europe/Istanbul") is used for day boundaries and display.
/// </summary>
public static class TimeZones
{
    public const string Turkey = "Europe/Istanbul";

    public static TimeZoneInfo Find(string ianaOrWindowsId)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(ianaOrWindowsId, out var tz))
        {
            return tz;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaOrWindowsId, out var windowsId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out tz))
        {
            return tz;
        }

        throw new TimeZoneNotFoundException($"Unknown time zone '{ianaOrWindowsId}'.");
    }

    /// <summary>
    /// Converts a local wall-clock time to UTC. Times inside a DST gap (non-existent) are moved forward by the gap;
    /// ambiguous times (DST fall-back) resolve to the first occurrence (daylight offset).
    /// </summary>
    public static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            var adjustment = zone.GetAdjustmentRules()
                .Where(r => r.DateStart <= unspecified && unspecified <= r.DateEnd)
                .Select(r => r.DaylightDelta)
                .FirstOrDefault(TimeSpan.FromHours(1));
            unspecified = unspecified.Add(adjustment);
        }

        if (zone.IsAmbiguousTime(unspecified))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(unspecified);
            var max = offsets.Max();
            return DateTime.SpecifyKind(unspecified - max, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(Guard.Utc(utc), zone);

    /// <summary>UTC range covering the whole local calendar day (23 or 25 hours on DST change days).</summary>
    public static UtcRange DayRange(DateOnly day, TimeZoneInfo zone) =>
        new(ToUtc(day.ToDateTime(TimeOnly.MinValue), zone), ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));

    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone) => DateOnly.FromDateTime(ToLocal(utc, zone));
}
