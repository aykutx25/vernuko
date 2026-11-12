using Vernuko.Appointments;

namespace Vernuko.Reporting;

public sealed record DailySummary(int Total, int Completed, int Open, int Cancelled, int NoShow, int Postponed)
{
    /// <summary>Completed share of appointments that were not cancelled/postponed (0–100).</summary>
    public int CompletionPercent => Scheduled == 0 ? 0 : (int)Math.Round(Completed * 100.0 / Scheduled);

    public int Scheduled => Total - Cancelled - Postponed;

    public static DailySummary From(IEnumerable<AppointmentStatus> statuses)
    {
        var list = statuses.ToList();
        return new DailySummary(
            list.Count,
            list.Count(s => s == AppointmentStatus.Completed),
            list.Count(AppointmentStatusRules.IsOpen),
            list.Count(s => s == AppointmentStatus.Cancelled),
            list.Count(s => s == AppointmentStatus.NoShow),
            list.Count(s => s == AppointmentStatus.Postponed));
    }
}

public sealed record AppointmentRates(int Total, double CancellationRate, double NoShowRate)
{
    public static AppointmentRates From(IEnumerable<AppointmentStatus> statuses)
    {
        var list = statuses.ToList();
        if (list.Count == 0)
        {
            return new AppointmentRates(0, 0, 0);
        }

        return new AppointmentRates(
            list.Count,
            list.Count(s => s == AppointmentStatus.Cancelled) / (double)list.Count,
            list.Count(s => s == AppointmentStatus.NoShow) / (double)list.Count);
    }
}
