namespace Vernuko.Appointments;

/// <summary>Appointment lifecycle. Stored by name.</summary>
public enum AppointmentStatus
{
    Created,
    PendingConfirmation,
    Confirmed,
    Arrived,
    Waiting,
    InProgress,
    Completed,
    Postponed,
    Cancelled,
    NoShow,
}

public static class AppointmentStatusRules
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> Transitions = new()
    {
        [AppointmentStatus.Created] =
        [
            AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed, AppointmentStatus.Arrived,
            AppointmentStatus.InProgress, AppointmentStatus.Completed, AppointmentStatus.Postponed,
            AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
        ],
        [AppointmentStatus.PendingConfirmation] =
        [
            AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.Postponed,
            AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
        ],
        [AppointmentStatus.Confirmed] =
        [
            AppointmentStatus.Arrived, AppointmentStatus.InProgress, AppointmentStatus.Completed,
            AppointmentStatus.Postponed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow,
        ],
        [AppointmentStatus.Arrived] = [AppointmentStatus.Waiting, AppointmentStatus.InProgress, AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Waiting] = [AppointmentStatus.InProgress, AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        [AppointmentStatus.InProgress] = [AppointmentStatus.Completed],

        // Re-opening a completed appointment is a correction: it requires a reason and restores any consumed session.
        [AppointmentStatus.Completed] = [AppointmentStatus.InProgress],
        [AppointmentStatus.Postponed] = [AppointmentStatus.Confirmed, AppointmentStatus.Cancelled],

        // Undo for accidental cancellation / late arrival after being marked as no-show. Both re-check conflicts.
        [AppointmentStatus.Cancelled] = [AppointmentStatus.Created],
        [AppointmentStatus.NoShow] = [AppointmentStatus.Arrived],
    };

    public static IReadOnlyList<AppointmentStatus> NextStatuses(AppointmentStatus from) =>
        Transitions.TryGetValue(from, out var next) ? next : [];

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to) => NextStatuses(from).Contains(to);

    /// <summary>Statuses that occupy staff/rooms/devices on the calendar.</summary>
    public static bool HoldsResources(AppointmentStatus status) => status is not (AppointmentStatus.Postponed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow);

    /// <summary>Statuses that can only be set once the appointment start time has been reached.</summary>
    /// <summary>
    /// How early treatment may start or be completed: customers arrive early and a free room can be used, but an
    /// appointment for later today or tomorrow must not be completed by mistake (it consumes a package session).
    /// </summary>
    public static readonly TimeSpan EarlyStartTolerance = TimeSpan.FromMinutes(60);

    /// <summary>
    /// True when <paramref name="to"/> cannot be chosen yet: "Gelmedi" only after the start time, "İşleme başla" and
    /// "Tamamla" from <see cref="EarlyStartTolerance"/> before it. Start and now must use the same clock (both UTC or
    /// both local business time).
    /// </summary>
    public static bool IsTooEarly(AppointmentStatus to, DateTime start, DateTime now) => to switch
    {
        AppointmentStatus.NoShow => now < start,
        AppointmentStatus.InProgress or AppointmentStatus.Completed => now < start - EarlyStartTolerance,
        _ => false,
    };

    public static string TooEarlyMessage(AppointmentStatus to) => to == AppointmentStatus.NoShow
        ? "Randevu saati geçmeden \"Gelmedi\" işaretlenemez."
        : "İşleme başlama ve tamamlama en erken randevu saatinden 1 saat önce yapılabilir.";

    public static bool RequiresReason(AppointmentStatus from, AppointmentStatus to) =>
        to is AppointmentStatus.Cancelled || (from == AppointmentStatus.Completed && to == AppointmentStatus.InProgress);

    public static bool IsOpen(AppointmentStatus status) => status is AppointmentStatus.Created or AppointmentStatus.PendingConfirmation
        or AppointmentStatus.Confirmed or AppointmentStatus.Arrived or AppointmentStatus.Waiting or AppointmentStatus.InProgress;

    public static bool CanReschedule(AppointmentStatus status) => status is AppointmentStatus.Created or AppointmentStatus.PendingConfirmation
        or AppointmentStatus.Confirmed or AppointmentStatus.Postponed;

    public static string DisplayName(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Created => "Oluşturuldu",
        AppointmentStatus.PendingConfirmation => "Onay bekliyor",
        AppointmentStatus.Confirmed => "Onaylandı",
        AppointmentStatus.Arrived => "Müşteri geldi",
        AppointmentStatus.Waiting => "Bekliyor",
        AppointmentStatus.InProgress => "İşlemde",
        AppointmentStatus.Completed => "Tamamlandı",
        AppointmentStatus.Postponed => "Ertelendi",
        AppointmentStatus.Cancelled => "İptal edildi",
        AppointmentStatus.NoShow => "Gelmedi",
        _ => status.ToString(),
    };

    /// <summary>Verb used on action buttons ("Tamamla", "İptal et" ...).</summary>
    public static string ActionName(AppointmentStatus from, AppointmentStatus to) => (from, to) switch
    {
        (AppointmentStatus.Completed, AppointmentStatus.InProgress) => "Tamamlamayı geri al",
        (AppointmentStatus.Cancelled, AppointmentStatus.Created) => "İptali geri al",
        (AppointmentStatus.NoShow, AppointmentStatus.Arrived) => "Geç geldi olarak işaretle",
        (_, AppointmentStatus.PendingConfirmation) => "Onaya gönder",
        (_, AppointmentStatus.Confirmed) => "Onayla",
        (_, AppointmentStatus.Arrived) => "Müşteri geldi",
        (_, AppointmentStatus.Waiting) => "Beklemeye al",
        (_, AppointmentStatus.InProgress) => "İşleme başla",
        (_, AppointmentStatus.Completed) => "Tamamla",
        (_, AppointmentStatus.Postponed) => "Ertele",
        (_, AppointmentStatus.Cancelled) => "İptal et",
        (_, AppointmentStatus.NoShow) => "Gelmedi",
        _ => DisplayName(to),
    };
}
