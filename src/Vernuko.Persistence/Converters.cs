using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vernuko.Core;

namespace Vernuko.Persistence;

/// <summary>All DateTimes are stored as UTC and come back with Kind=Utc.</summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.ToUniversalTime(), DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public sealed class WeeklyScheduleConverter : ValueConverter<WeeklySchedule, string>
{
    public WeeklyScheduleConverter()
        : base(v => Serialize(v), v => Deserialize(v))
    {
    }

    public static string Serialize(WeeklySchedule schedule) =>
        JsonSerializer.Serialize(schedule.Days.ToDictionary(
            kv => kv.Key.ToString(),
            kv => kv.Value.OrderBy(i => i.Start).Select(i => $"{i.Start:HH\\:mm}-{i.End:HH\\:mm}").ToArray()));

    public static WeeklySchedule Deserialize(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? [];
        var schedule = new WeeklySchedule();
        foreach (var (day, intervals) in raw)
        {
            if (!Enum.TryParse<DayOfWeek>(day, out var dow))
            {
                continue;
            }

            schedule.Days[dow] = intervals
                .Select(s => s.Split('-'))
                .Where(p => p.Length == 2)
                .Select(p => new TimeInterval(TimeOnly.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), TimeOnly.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
        }

        return schedule;
    }
}

public sealed class WeeklyScheduleComparer : ValueComparer<WeeklySchedule>
{
    public WeeklyScheduleComparer()
        : base(
            (a, b) => WeeklyScheduleConverter.Serialize(a!) == WeeklyScheduleConverter.Serialize(b!),
            v => WeeklyScheduleConverter.Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => WeeklyScheduleConverter.Deserialize(WeeklyScheduleConverter.Serialize(v)))
    {
    }
}

public sealed class NullableWeeklyScheduleConverter : ValueConverter<WeeklySchedule?, string?>
{
    public NullableWeeklyScheduleConverter()
        : base(v => v == null ? null : WeeklyScheduleConverter.Serialize(v), v => v == null ? null : WeeklyScheduleConverter.Deserialize(v))
    {
    }
}

public sealed class NullableWeeklyScheduleComparer : ValueComparer<WeeklySchedule?>
{
    public NullableWeeklyScheduleComparer()
        : base(
            (a, b) => (a == null ? null : WeeklyScheduleConverter.Serialize(a)) == (b == null ? null : WeeklyScheduleConverter.Serialize(b)),
            v => v == null ? 0 : WeeklyScheduleConverter.Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => v == null ? null : WeeklyScheduleConverter.Deserialize(WeeklyScheduleConverter.Serialize(v)))
    {
    }
}
