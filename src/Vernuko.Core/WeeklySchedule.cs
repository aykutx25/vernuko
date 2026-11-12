namespace Vernuko.Core;

/// <summary>A local-time interval within a day, e.g. 09:00–12:30.</summary>
public sealed record TimeInterval(TimeOnly Start, TimeOnly End)
{
    public bool IsValid => End > Start;

    public TimeSpan Duration => End - Start;

    public override string ToString() => $"{Start:HH\\:mm}–{End:HH\\:mm}";
}

/// <summary>
/// Weekly working hours: for each day, a list of open intervals (breaks are the gaps between intervals).
/// An empty list means closed.
/// </summary>
public sealed class WeeklySchedule
{
    public Dictionary<DayOfWeek, List<TimeInterval>> Days { get; init; } = [];

    public IReadOnlyList<TimeInterval> For(DayOfWeek day) =>
        Days.TryGetValue(day, out var intervals) ? intervals : [];

    public bool IsOpen(DayOfWeek day) => For(day).Count > 0;

    public static WeeklySchedule Create(IEnumerable<DayOfWeek> openDays, TimeOnly open, TimeOnly close, TimeInterval? breakInterval = null)
    {
        var schedule = new WeeklySchedule();
        foreach (var day in openDays)
        {
            var intervals = new List<TimeInterval>();
            if (breakInterval is not null && breakInterval.Start > open && breakInterval.End < close)
            {
                intervals.Add(new TimeInterval(open, breakInterval.Start));
                intervals.Add(new TimeInterval(breakInterval.End, close));
            }
            else
            {
                intervals.Add(new TimeInterval(open, close));
            }

            schedule.Days[day] = intervals;
        }

        return schedule;
    }

    public static WeeklySchedule DefaultBusinessWeek() =>
        Create(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
            new TimeOnly(9, 0),
            new TimeOnly(19, 0),
            new TimeInterval(new TimeOnly(12, 30), new TimeOnly(13, 30)));

    public Result Validate()
    {
        foreach (var (day, intervals) in Days)
        {
            var ordered = intervals.OrderBy(i => i.Start).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (!ordered[i].IsValid)
                {
                    return Error.Validation("schedule.interval", $"{DayNames.Turkish(day)} için bitiş saati başlangıçtan sonra olmalıdır.");
                }

                if (i > 0 && ordered[i].Start < ordered[i - 1].End)
                {
                    return Error.Validation("schedule.overlap", $"{DayNames.Turkish(day)} için çalışma aralıkları çakışıyor.");
                }
            }
        }

        return Result.Success();
    }

    public WeeklySchedule Clone() => new()
    {
        Days = Days.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
    };
}

public static class DayNames
{
    public static string Turkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => day.ToString(),
    };

    /// <summary>Monday-first week order used across the UI.</summary>
    public static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];
}
