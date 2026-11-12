using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Core;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

public sealed partial class DayHoursRow(DayOfWeek day) : ObservableObject
{
    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private string _start = "09:00";

    [ObservableProperty]
    private string _end = "19:00";

    public DayOfWeek Day { get; } = day;

    public string Name => DayNames.Turkish(Day);
}

public sealed partial class ServiceChoice(ServiceDto service, bool selected) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = selected;

    public ServiceDto Service { get; } = service;
}

/// <summary>Staff list, personal working hours, services they deliver, leave and business closures.</summary>
public sealed partial class StaffViewModel(CatalogService catalog, IDialogService dialogs, TimeProvider time) : PageViewModel
{
    [ObservableProperty]
    private StaffDto? _selected;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _staffTitle;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _useBusinessHours = true;

    [ObservableProperty]
    private DateTime _leaveFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime _leaveTo = DateTime.Today;

    [ObservableProperty]
    private string? _leaveNote;

    [ObservableProperty]
    private bool _leaveIsBusinessClosure;

    public override string Title => "Personel";

    public ObservableCollection<StaffDto> Staff { get; } = [];

    public ObservableCollection<DayHoursRow> Hours { get; } = new(DayNames.WeekOrder.Select(d => new DayHoursRow(d)));

    public ObservableCollection<ServiceChoice> ServiceChoices { get; } = [];

    public ObservableCollection<ScheduleExceptionDto> Exceptions { get; } = [];

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        await ReloadAsync();
        New();
    });

    partial void OnSelectedChanged(StaffDto? value)
    {
        if (value is null)
        {
            return;
        }

        DisplayName = value.DisplayName;
        StaffTitle = value.Title;
        Phone = value.Phone;
        IsActive = value.IsActive;
        UseBusinessHours = value.WorkingHours is null;
        foreach (var row in Hours)
        {
            var intervals = value.WorkingHours?.For(row.Day) ?? [];
            row.IsWorking = intervals.Count > 0;
            row.Start = intervals.Count > 0 ? intervals.Min(i => i.Start).ToString("HH:mm", TurkishText.Culture) : "09:00";
            row.End = intervals.Count > 0 ? intervals.Max(i => i.End).ToString("HH:mm", TurkishText.Culture) : "19:00";
        }

        foreach (var c in ServiceChoices)
        {
            c.IsSelected = value.ServiceIds.Contains(c.Service.Id);
        }
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        DisplayName = string.Empty;
        StaffTitle = null;
        Phone = null;
        IsActive = true;
        UseBusinessHours = true;
        foreach (var c in ServiceChoices)
        {
            c.IsSelected = false;
        }
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        WeeklySchedule? hours = null;
        if (!UseBusinessHours)
        {
            hours = new WeeklySchedule();
            foreach (var row in Hours.Where(h => h.IsWorking))
            {
                if (Formatting.ParseTime(row.Start) is not { } s || Formatting.ParseTime(row.End) is not { } e || e <= s)
                {
                    ErrorMessage = $"{row.Name} için saatleri SS:DD biçiminde girin.";
                    return;
                }

                hours.Days[row.Day] = [new TimeInterval(s, e)];
            }
        }

        var services = ServiceChoices.Where(c => c.IsSelected).Select(c => c.Service.Id).ToList();
        var r = await catalog.SaveStaffAsync(new StaffDto(Selected?.Id ?? Guid.Empty, DisplayName, StaffTitle, Phone, Selected?.Color, IsActive, hours, services));
        if (Check(r))
        {
            await ReloadAsync();
            Selected = Staff.FirstOrDefault(s => s.Id == r.Value);
            InfoMessage = "Personel kaydedildi.";
        }
    });

    [RelayCommand]
    private Task AddLeaveAsync() => RunAsync(async () =>
    {
        if (!LeaveIsBusinessClosure && Selected is null)
        {
            ErrorMessage = "İzin eklemek için önce listeden personel seçin.";
            return;
        }

        if (LeaveTo.Date < LeaveFrom.Date)
        {
            ErrorMessage = "Bitiş tarihi başlangıçtan önce olamaz.";
            return;
        }

        var scope = LeaveIsBusinessClosure ? ScheduleExceptionScope.Business : ScheduleExceptionScope.Staff;
        var kind = LeaveIsBusinessClosure ? ScheduleExceptionKind.Closure : ScheduleExceptionKind.Leave;
        var r = await catalog.AddExceptionAsync(scope, LeaveIsBusinessClosure ? null : Selected!.Id, kind, LeaveFrom.Date, LeaveTo.Date.AddDays(1), LeaveNote);
        if (Check(r))
        {
            LeaveNote = null;
            await LoadExceptionsAsync();
            InfoMessage = LeaveIsBusinessClosure ? "Kapalı gün eklendi." : "İzin eklendi. Mevcut randevular otomatik değiştirilmez; lütfen kontrol edin.";
        }
    });

    [RelayCommand]
    private Task RemoveExceptionAsync(ScheduleExceptionDto item) => RunAsync(async () =>
    {
        if (await dialogs.ConfirmAsync("Kaldır", $"{item.TargetName ?? "İşletme"}: {Formatting.ShortDate(item.StartLocal)} kaydı kaldırılsın mı?", "Kaldır") &&
            Check(await catalog.RemoveExceptionAsync(item.Id)))
        {
            await LoadExceptionsAsync();
        }
    });

    private async Task ReloadAsync()
    {
        Staff.Clear();
        foreach (var s in (await catalog.GetStaffAsync(includeInactive: true)).OrEmpty())
        {
            Staff.Add(s);
        }

        if (ServiceChoices.Count == 0)
        {
            foreach (var s in (await catalog.GetServicesAsync()).OrEmpty())
            {
                ServiceChoices.Add(new ServiceChoice(s, false));
            }
        }

        await LoadExceptionsAsync();
    }

    private async Task LoadExceptionsAsync()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        Exceptions.Clear();
        foreach (var e in (await catalog.GetExceptionsAsync(today, today.AddDays(120))).OrEmpty())
        {
            Exceptions.Add(e);
        }
    }
}
