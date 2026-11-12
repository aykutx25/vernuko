using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

public sealed record ReportLine(string Name, int Count);

public sealed partial class ReportsViewModel(ReportService reports, TimeProvider time) : PageViewModel
{
    [ObservableProperty]
    private DateTime _from = new(time.GetLocalNow().Year, time.GetLocalNow().Month, 1);

    [ObservableProperty]
    private DateTime _to = time.GetLocalNow().Date;

    [ObservableProperty]
    private PeriodReport? _report;

    public override string Title => "Raporlar";

    public IReadOnlyList<ReportLine> ByStaff => Report?.ByStaff.Select(x => new ReportLine(x.Name, x.Count)).ToList() ?? [];

    public IReadOnlyList<ReportLine> ByService => Report?.ByService.Select(x => new ReportLine(x.Name, x.Count)).ToList() ?? [];

    public string CancellationText => Report is null ? "–" : $"%{Report.CancellationRate * 100:0.#}";

    public string NoShowText => Report is null ? "–" : $"%{Report.NoShowRate * 100:0.#}";

    public string CollectedText => Report?.CollectedMinor is { } c ? Formatting.Money(c) : "Yetki gerekli";

    public string OutstandingText => Report?.OutstandingMinor is { } o ? Formatting.Money(o) : "Yetki gerekli";

    public override Task OnNavigatedToAsync(object? parameter) => LoadAsync();

    partial void OnReportChanged(PeriodReport? value)
    {
        OnPropertyChanged(nameof(ByStaff));
        OnPropertyChanged(nameof(ByService));
        OnPropertyChanged(nameof(CancellationText));
        OnPropertyChanged(nameof(NoShowText));
        OnPropertyChanged(nameof(CollectedText));
        OnPropertyChanged(nameof(OutstandingText));
    }

    [RelayCommand]
    private Task RangeAsync(string range)
    {
        var today = time.GetLocalNow().Date;
        (From, To) = range switch
        {
            "today" => (today, today),
            "week" => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today),
            "lastMonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            _ => (new DateTime(today.Year, today.Month, 1), today),
        };
        return LoadAsync();
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        if (To < From)
        {
            ErrorMessage = "Bitiş tarihi başlangıçtan önce olamaz.";
            return;
        }

        var r = await reports.GetPeriodReportAsync(DateOnly.FromDateTime(From), DateOnly.FromDateTime(To));
        if (Check(r))
        {
            Report = r.Value;
        }
    });
}
