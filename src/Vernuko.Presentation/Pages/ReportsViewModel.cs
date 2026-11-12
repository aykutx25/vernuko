using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed record ReportLine(string Name, int Count);

public sealed partial class ReportsViewModel(IReportService reports, ICommissionService commissions, TimeProvider time) : PageViewModel, IShellAware
{
    [ObservableProperty]
    private CommissionReport? _commission;

    [ObservableProperty]
    private DateTime _from = new(time.GetLocalNow().Year, time.GetLocalNow().Month, 1);

    [ObservableProperty]
    private DateTime _to = time.GetLocalNow().Date;

    [ObservableProperty]
    private PeriodReport? _report;

    public override string Title => "Raporlar";

    public ShellViewModel? Shell { get; set; }

    /// <summary>Commissions are financial data: only users with the financial report permission see them.</summary>
    public bool ShowCommission => Shell?.Has(Permission.ReportsFinancial) ?? false;

    public string CommissionTotalText => Commission is null ? "–" : Formatting.Money(Commission.GrandTotalMinor);

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

        if (ShowCommission)
        {
            var c = await commissions.GetReportAsync(DateOnly.FromDateTime(From), DateOnly.FromDateTime(To));
            Commission = c.IsSuccess ? c.Value : null;
        }

        OnPropertyChanged(nameof(ShowCommission));
        OnPropertyChanged(nameof(CommissionTotalText));
    });
}
