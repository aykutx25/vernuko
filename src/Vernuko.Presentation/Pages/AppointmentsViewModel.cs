using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Messaging;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;
using Vernuko.Reporting;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public enum CalendarView
{
    Day,
    Week,
    Month,
}

public abstract class AgendaRow : ObservableObject
{
    public abstract DateTime Start { get; }

    public string TimeText => Formatting.Time(Start);
}

public sealed class BreakRow(DateTime start, DateTime end, string title) : AgendaRow
{
    public override DateTime Start => start;

    public string Title => title;

    public string RangeText => $"{Formatting.Time(start)} – {Formatting.Time(end)}";
}

public sealed record StatusAction(string Label, AppointmentStatus Target, bool IsDestructive);

public sealed partial class AppointmentRow(AppointmentListItem item, bool isCurrent) : AgendaRow
{
    public AppointmentListItem Item { get; } = item;

    public override DateTime Start => Item.StartLocal;

    public bool IsCurrent { get; } = isCurrent;

    public string Customer => Item.CustomerName;

    public string? Phone => Item.CustomerPhone;

    public string ServiceLine => string.Join(" · ", new[] { Item.ServiceName, Item.SessionLabel, $"({Item.DurationMinutes} dk)" }.Where(s => !string.IsNullOrEmpty(s)));

    public string Staff => Item.StaffName;

    public string? Resources => Item.ResourceNames;

    public string? BodyArea => Item.BodyArea;

    public AppointmentStatus Status => Item.Status;

    public string StatusName => Item.StatusName;

    /// <summary>Visual group used for the status colour: done, active, waiting, cancelled.</summary>
    public string StatusGroup => Item.Status switch
    {
        AppointmentStatus.Completed => "Done",
        AppointmentStatus.InProgress or AppointmentStatus.Arrived or AppointmentStatus.Waiting => "Active",
        AppointmentStatus.Cancelled or AppointmentStatus.NoShow or AppointmentStatus.Postponed => "Off",
        AppointmentStatus.PendingConfirmation => "Pending",
        _ => "Planned",
    };

    public IReadOnlyList<StatusAction> Actions { get; } = item.NextStatuses
        .Select(s => new StatusAction(AppointmentStatusRules.ActionName(item.Status, s), s, s is AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
        .ToList();

    public bool CanReschedule => Item.CanReschedule;
}

public sealed class WeekDayColumn(DateOnly date, bool isToday, IReadOnlyList<AppointmentListItem> items)
{
    public DateOnly Date { get; } = date;

    public string Title => Date.ToDateTime(TimeOnly.MinValue).ToString("ddd d MMM", TurkishText.Culture);

    public bool IsToday { get; } = isToday;

    public IReadOnlyList<AppointmentListItem> Items { get; } = items;
}

public sealed class MonthCell(DateOnly date, bool inMonth, bool isToday, int count, int completed)
{
    public DateOnly Date { get; } = date;

    public int DayNumber => Date.Day;

    public bool InMonth { get; } = inMonth;

    public bool IsToday { get; } = isToday;

    public int Count { get; } = count;

    public int Completed { get; } = completed;

    public string CountText => Count == 0 ? string.Empty : $"{Count} randevu";
}

/// <summary>The main working screen of the front desk.</summary>
public sealed partial class AppointmentsViewModel : PageViewModel, IShellAware
{
    private readonly AppointmentService _appointments;
    private readonly CatalogService _catalog;
    private readonly MessagingService _messaging;
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogs;
    private readonly IPlatformServices _platform;
    private readonly TimeProvider _time;
    private bool _loaded;
    private bool _suspendReload;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateTitle))]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private CalendarView _view = CalendarView.Day;

    [ObservableProperty]
    private Option<Guid?>? _selectedStaff;

    [ObservableProperty]
    private Option<Guid?>? _selectedResource;

    [ObservableProperty]
    private Option<Guid?>? _selectedService;

    [ObservableProperty]
    private DailySummary _summary = DailySummary.From([]);

    [ObservableProperty]
    private int _activeStaffCount;

    [ObservableProperty]
    private AppointmentEditorViewModel? _editor;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isClosedDay;

    public AppointmentsViewModel(AppointmentService appointments, CatalogService catalog, MessagingService messaging, IServiceProvider services, IDialogService dialogs, IPlatformServices platform, TimeProvider time)
    {
        _appointments = appointments;
        _catalog = catalog;
        _messaging = messaging;
        _services = services;
        _dialogs = dialogs;
        _platform = platform;
        _time = time;
        _selectedDate = time.GetLocalNow().Date;
    }

    public override string Title => "Randevular";

    public ShellViewModel? Shell { get; set; }

    public string DateTitle => Formatting.LongDate(SelectedDate);

    public ObservableCollection<AgendaRow> Rows { get; } = [];

    public ObservableCollection<WeekDayColumn> WeekDays { get; } = [];

    public ObservableCollection<MonthCell> MonthCells { get; } = [];

    public ObservableCollection<AppointmentListItem> Upcoming { get; } = [];

    public ObservableCollection<Option<Guid?>> StaffOptions { get; } = [];

    public ObservableCollection<Option<Guid?>> ResourceOptions { get; } = [];

    public ObservableCollection<Option<Guid?>> ServiceOptions { get; } = [];

    public bool IsEditorOpen => Editor is not null;

    public bool CanManage => Shell?.Has(Permission.AppointmentsManage) ?? true;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (!_loaded)
        {
            await LoadFiltersAsync();
            _loaded = true;
        }

        await RefreshAsync();
        if (parameter is OpenNewAppointment open)
        {
            await OpenEditorAsync(open.CustomerId);
        }
    }

    partial void OnSelectedDateChanged(DateTime value) => _ = ReloadAsync();

    partial void OnViewChanged(CalendarView value) => _ = ReloadAsync();

    partial void OnSelectedStaffChanged(Option<Guid?>? value) => _ = ReloadAsync();

    partial void OnSelectedResourceChanged(Option<Guid?>? value) => _ = ReloadAsync();

    partial void OnSelectedServiceChanged(Option<Guid?>? value) => _ = ReloadAsync();

    partial void OnEditorChanged(AppointmentEditorViewModel? value) => OnPropertyChanged(nameof(IsEditorOpen));

    [RelayCommand]
    private void PreviousPeriod() => SelectedDate = View switch
    {
        CalendarView.Week => SelectedDate.AddDays(-7),
        CalendarView.Month => SelectedDate.AddMonths(-1),
        _ => SelectedDate.AddDays(-1),
    };

    [RelayCommand]
    private void NextPeriod() => SelectedDate = View switch
    {
        CalendarView.Week => SelectedDate.AddDays(7),
        CalendarView.Month => SelectedDate.AddMonths(1),
        _ => SelectedDate.AddDays(1),
    };

    [RelayCommand]
    private void Today() => SelectedDate = _time.GetLocalNow().Date;

    [RelayCommand]
    private void SetView(CalendarView view) => View = view;

    [RelayCommand]
    private void OpenDay(MonthCell cell)
    {
        _suspendReload = true;
        SelectedDate = cell.Date.ToDateTime(TimeOnly.MinValue);
        _suspendReload = false;
        View = CalendarView.Day;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _suspendReload = true;
        SelectedStaff = StaffOptions.FirstOrDefault();
        SelectedResource = ResourceOptions.FirstOrDefault();
        SelectedService = ServiceOptions.FirstOrDefault();
        _suspendReload = false;
        _ = ReloadAsync();
    }

    [RelayCommand]
    public Task RefreshAsync() => RunAsync(async () =>
    {
        var filter = new AppointmentFilter(SelectedStaff?.Value, SelectedResource?.Value, SelectedService?.Value);
        var day = DateOnly.FromDateTime(SelectedDate);
        var nowLocal = _time.GetLocalNow().DateTime;
        switch (View)
        {
            case CalendarView.Day:
            {
                var agenda = await _appointments.GetDayAsync(day, filter);
                if (!Check(agenda))
                {
                    return;
                }

                Rows.Clear();
                var rows = new List<AgendaRow>();
                var current = agenda.Value.Items.FirstOrDefault(i => i.StartLocal <= nowLocal && nowLocal < i.EndLocal && AppointmentStatusRules.IsOpen(i.Status));
                rows.AddRange(agenda.Value.Items.Select(i => new AppointmentRow(i, i == current)));
                rows.AddRange(agenda.Value.Breaks.Select(b => new BreakRow(day.ToDateTime(b.Start), day.ToDateTime(b.End), "Öğle Arası")));
                foreach (var r in rows.OrderBy(r => r.Start))
                {
                    Rows.Add(r);
                }

                Summary = agenda.Value.Summary;
                ActiveStaffCount = agenda.Value.ActiveStaffCount;
                IsEmpty = agenda.Value.Items.Count == 0;
                IsClosedDay = !agenda.Value.IsBusinessOpen;
                break;
            }

            case CalendarView.Week:
            {
                var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
                var items = await _appointments.GetRangeAsync(monday, monday.AddDays(6), filter);
                if (!Check(items))
                {
                    return;
                }

                WeekDays.Clear();
                var today = DateOnly.FromDateTime(nowLocal);
                for (var i = 0; i < 7; i++)
                {
                    var d = monday.AddDays(i);
                    WeekDays.Add(new WeekDayColumn(d, d == today, items.Value.Where(x => DateOnly.FromDateTime(x.StartLocal) == d).ToList()));
                }

                IsEmpty = items.Value.Count == 0;
                break;
            }

            case CalendarView.Month:
            {
                var first = new DateOnly(day.Year, day.Month, 1);
                var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
                var items = await _appointments.GetRangeAsync(gridStart, gridStart.AddDays(41), filter);
                if (!Check(items))
                {
                    return;
                }

                MonthCells.Clear();
                var today = DateOnly.FromDateTime(nowLocal);
                for (var i = 0; i < 42; i++)
                {
                    var d = gridStart.AddDays(i);
                    var dayItems = items.Value.Where(x => DateOnly.FromDateTime(x.StartLocal) == d && x.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.Postponed)).ToList();
                    MonthCells.Add(new MonthCell(d, d.Month == first.Month, d == today, dayItems.Count, dayItems.Count(x => x.Status == AppointmentStatus.Completed)));
                }

                IsEmpty = items.Value.Count == 0;
                break;
            }
        }

        var upcoming = await _appointments.GetUpcomingAsync(5);
        if (upcoming.IsSuccess)
        {
            Upcoming.Clear();
            foreach (var u in upcoming.Value)
            {
                Upcoming.Add(u);
            }
        }
    });

    [RelayCommand]
    private Task NewAppointmentAsync() => OpenEditorAsync(null);

    public async Task OpenEditorAsync(Guid? customerId)
    {
        var editor = _services.GetRequiredService<AppointmentEditorViewModel>();
        editor.Closed += async (_, saved) =>
        {
            Editor = null;
            if (saved)
            {
                await RefreshAsync();
                InfoMessage = "Randevu kaydedildi.";
            }
        };
        Editor = editor;
        await editor.InitializeForCreateAsync(DateOnly.FromDateTime(SelectedDate), customerId);
    }

    [RelayCommand]
    private async Task RescheduleAsync(AppointmentRow row)
    {
        var editor = _services.GetRequiredService<AppointmentEditorViewModel>();
        editor.Closed += async (_, saved) =>
        {
            Editor = null;
            if (saved)
            {
                await RefreshAsync();
                InfoMessage = "Randevu yeni saate taşındı.";
            }
        };
        Editor = editor;
        await editor.InitializeForRescheduleAsync(row.Item);
    }

    [RelayCommand]
    private Task ChangeStatusAsync(object? parameter) => RunAsync(async () =>
    {
        if (parameter is not object[] { Length: 2 } args || args[0] is not AppointmentRow row || args[1] is not StatusAction action)
        {
            return;
        }

        string? reason = null;
        if (AppointmentStatusRules.RequiresReason(row.Status, action.Target))
        {
            reason = await _dialogs.PromptAsync(action.Label, action.Target == AppointmentStatus.Cancelled
                ? $"{row.Customer} — {row.TimeText} randevusu iptal edilecek. Seans hakkı düşülmez. İptal nedeni:"
                : "Bu işlem tamamlama kaydını geri alır; seans hakkı iade edilir. Açıklama:");
            if (reason is null)
            {
                return;
            }
        }
        else if (action.IsDestructive && !await _dialogs.ConfirmAsync(action.Label, $"{row.Customer} — {row.TimeText}: \"{action.Label}\" olarak işaretlensin mi?", action.Label, destructive: true))
        {
            return;
        }

        var result = await _appointments.ChangeStatusAsync(row.Item.Id, action.Target, reason);
        if (Check(result))
        {
            await RefreshAsync();
            InfoMessage = $"{row.Customer}: {AppointmentStatusRules.DisplayName(action.Target)}";
        }
    }, clearMessages: true);

    [RelayCommand]
    private Task SendWhatsAppAsync(AppointmentRow row) => RunAsync(async () =>
    {
        var message = await _messaging.PrepareWhatsAppAsync(row.Item.Id, MessagePurpose.AppointmentReminder);
        if (Check(message))
        {
            _platform.OpenUri(message.Value.WhatsAppLink);
            InfoMessage = "WhatsApp açıldı. Mesajı kontrol edip gönderin.";
        }
    });

    [RelayCommand]
    private void TakePayment(AppointmentRow row)
    {
        var dialog = _services.GetRequiredService<PaymentDialogViewModel>();
        dialog.Closed += async (_, saved) =>
        {
            Shell?.CloseOverlay();
            if (saved)
            {
                InfoMessage = "Ödeme kaydedildi.";
                await RefreshAsync();
            }
        };
        dialog.Initialize(row.Item.CustomerId, row.Item.CustomerName, row.Item.Id, row.Item.PriceMinor);
        Shell?.ShowOverlay(dialog);
    }

    [RelayCommand]
    private Task GoAsync(string key) => Shell?.NavigateAsync(key) ?? Task.CompletedTask;

    [RelayCommand]
    private void CloseEditor() => Editor?.CancelCommand.Execute(null);

    [RelayCommand]
    private Task OpenCustomerAsync(AppointmentRow row) => Shell?.NavigateAsync("customers", new OpenCustomer(row.Item.CustomerId)) ?? Task.CompletedTask;

    private async Task ReloadAsync()
    {
        if (_loaded && !_suspendReload)
        {
            await RefreshAsync();
        }
    }

    private async Task LoadFiltersAsync()
    {
        _suspendReload = true;
        try
        {
            StaffOptions.Clear();
            StaffOptions.Add(new Option<Guid?>("Tüm Personel", null));
            foreach (var s in (await _catalog.GetStaffAsync()).OrEmpty())
            {
                StaffOptions.Add(new Option<Guid?>(s.DisplayName, s.Id));
            }

            ResourceOptions.Clear();
            ResourceOptions.Add(new Option<Guid?>("Tüm Odalar", null));
            foreach (var r in (await _catalog.GetResourcesAsync()).OrEmpty())
            {
                ResourceOptions.Add(new Option<Guid?>(r.Name, r.Id));
            }

            ServiceOptions.Clear();
            ServiceOptions.Add(new Option<Guid?>("Tüm Hizmetler", null));
            foreach (var s in (await _catalog.GetServicesAsync()).OrEmpty())
            {
                ServiceOptions.Add(new Option<Guid?>(s.Name, s.Id));
            }

            SelectedStaff = StaffOptions[0];
            SelectedResource = ResourceOptions[0];
            SelectedService = ServiceOptions[0];
        }
        finally
        {
            _suspendReload = false;
        }
    }
}
