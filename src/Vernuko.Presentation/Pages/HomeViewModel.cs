using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Presentation.Common;
using Vernuko.Reporting;

namespace Vernuko.Presentation.Pages;

public sealed partial class HomeViewModel(IAppointmentService appointments, IBackupService backups, UserSession session, TimeProvider time) : PageViewModel, IShellAware
{
    [ObservableProperty]
    private DailySummary _today = DailySummary.From([]);

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _backupText = string.Empty;

    [ObservableProperty]
    private bool _backupWarning;

    public override string Title => "Ana Sayfa";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<AppointmentListItem> Upcoming { get; } = [];

    public string TodayText => Formatting.LongDate(time.GetLocalNow().Date);

    public bool CanManage => Shell?.Has(Security.Permission.AppointmentsManage) ?? true;

    public bool CanGoCustomers => Shell?.HasPage("customers") ?? true;

    public bool CanGoSettings => Shell?.HasPage("settings") ?? true;

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        OnPropertyChanged(nameof(CanManage));
        OnPropertyChanged(nameof(CanGoCustomers));
        OnPropertyChanged(nameof(CanGoSettings));
        var hour = time.GetLocalNow().Hour;
        Greeting = $"{(hour < 12 ? "Günaydın" : hour < 18 ? "İyi günler" : "İyi akşamlar")}, {session.DisplayName}";
        var day = await appointments.GetDayAsync(DateOnly.FromDateTime(time.GetLocalNow().Date), new AppointmentFilter());
        if (day.IsSuccess)
        {
            Today = day.Value.Summary;
        }

        Upcoming.Clear();
        foreach (var u in (await appointments.GetUpcomingAsync(8)).OrEmpty())
        {
            Upcoming.Add(u);
        }

        var status = await backups.GetStatusAsync();
        BackupWarning = !status.KeyConfigured || !status.RecoveryKeyConfirmed || status.LastSuccessUtc is null;
        BackupText = !status.KeyConfigured
            ? "Yedekleme etkin değil. Verilerinizi korumak için Ayarlar > Yedekleme bölümünden etkinleştirin."
            : status.LastSuccessUtc is { } last
                ? $"Son şifreli yedek: {Formatting.ShortDate(last.ToLocalTime())} {Formatting.Time(last.ToLocalTime())}"
                : "Henüz yedek alınmadı.";
        if (status.KeyConfigured && !status.RecoveryKeyConfirmed)
        {
            BackupText += " Kurtarma anahtarınızı doğrulamadınız.";
        }
    });

    [RelayCommand]
    private Task GoAsync(string key) => Shell?.NavigateAsync(key) ?? Task.CompletedTask;

    [RelayCommand]
    private Task NewAppointmentAsync() => Shell?.NavigateAsync("appointments", new OpenNewAppointment(null)) ?? Task.CompletedTask;
}
