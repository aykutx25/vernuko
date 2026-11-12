namespace Vernuko.Appointments;

public enum IssueSeverity
{
    /// <summary>Booking cannot be saved (e.g. double booking).</summary>
    Blocking,

    /// <summary>Booking can be saved only after the user explicitly confirms (e.g. outside working hours).</summary>
    RequiresConfirmation,
}

public sealed record SchedulingIssue(string Code, string Message, IssueSeverity Severity);
