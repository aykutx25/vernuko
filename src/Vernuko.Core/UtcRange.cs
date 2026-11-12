namespace Vernuko.Core;

/// <summary>Half-open UTC interval [Start, End).</summary>
public readonly record struct UtcRange
{
    public UtcRange(DateTime start, DateTime end)
    {
        Guard.Utc(start);
        Guard.Utc(end);
        if (end <= start)
        {
            throw new ArgumentException("End must be after start.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public TimeSpan Duration => End - Start;

    public bool Overlaps(UtcRange other) => Start < other.End && other.Start < End;

    public bool Contains(UtcRange other) => Start <= other.Start && other.End <= End;

    public bool Contains(DateTime instant) => Start <= instant && instant < End;

    public override string ToString() => $"[{Start:O} – {End:O})";
}
