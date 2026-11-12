using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Packages;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

public sealed partial class ResourceChoice(ResourceDto resource) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public ResourceDto Resource { get; } = resource;

    public string Label => $"{Resource.Name} ({Resource.KindName})";
}

public sealed record SlotChoice(TimeOnly Time)
{
    public string Label => Time.ToString("HH:mm", TurkishText.Culture);
}

/// <summary>
/// Side panel for creating or moving an appointment without leaving the calendar. Keyboard-first: customer search,
/// then service, staff, room, date and time; free slots are suggested as the selection changes.
/// </summary>
public sealed partial class AppointmentEditorViewModel : ViewModelBase
{
    private readonly AppointmentService _appointments;
    private readonly CustomerService _customers;
    private readonly CatalogService _catalog;
    private readonly PackageService _packages;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly Debouncer _searchDebounce = new(TimeSpan.FromMilliseconds(250));
    private Guid? _rescheduleId;
    private bool _initializing;

    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerListItem? _selectedCustomer;

    [ObservableProperty]
    private bool _isAddingCustomer;

    [ObservableProperty]
    private string _newFirstName = string.Empty;

    [ObservableProperty]
    private string _newLastName = string.Empty;

    [ObservableProperty]
    private string _newPhone = string.Empty;

    [ObservableProperty]
    private ServiceDto? _selectedService;

    [ObservableProperty]
    private StaffDto? _selectedStaff;

    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _durationMinutes = string.Empty;

    [ObservableProperty]
    private CustomerPackageDto? _selectedPackage;

    [ObservableProperty]
    private string? _bodyArea;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _showBodyArea;

    [ObservableProperty]
    private string _title = "Yeni Randevu";

    [ObservableProperty]
    private bool _noSlots;

    public AppointmentEditorViewModel(AppointmentService appointments, CustomerService customers, CatalogService catalog, PackageService packages, SettingsService settings, IDialogService dialogs)
    {
        _appointments = appointments;
        _customers = customers;
        _catalog = catalog;
        _packages = packages;
        _settings = settings;
        _dialogs = dialogs;
    }

    /// <summary>Raised when the panel closes; the argument tells whether something was saved.</summary>
    public event EventHandler<bool>? Closed;

    public bool IsReschedule => _rescheduleId is not null;

    public ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public ObservableCollection<ServiceDto> Services { get; } = [];

    public ObservableCollection<StaffDto> Staff { get; } = [];

    public ObservableCollection<ResourceChoice> Resources { get; } = [];

    public ObservableCollection<SlotChoice> Slots { get; } = [];

    public ObservableCollection<CustomerPackageDto> Packages { get; } = [];

    public bool HasPackages => Packages.Count > 0;

    public async Task InitializeForCreateAsync(DateOnly date, Guid? customerId)
    {
        _initializing = true;
        await LoadCatalogAsync();
        Date = date.ToDateTime(TimeOnly.MinValue);
        if (customerId is { } id && (await _customers.GetAsync(id)) is { IsSuccess: true } c)
        {
            SelectedCustomer = new CustomerListItem(c.Value.Id, c.Value.Number, c.Value.FullName, c.Value.Phone, c.Value.Email, c.Value.State, null);
            CustomerSearch = c.Value.FullName;
        }

        SelectedService = Services.FirstOrDefault();
        _initializing = false;
        await OnSelectionChangedAsync();
    }

    public async Task InitializeForRescheduleAsync(AppointmentListItem item)
    {
        _initializing = true;
        _rescheduleId = item.Id;
        Title = "Randevuyu Taşı";
        OnPropertyChanged(nameof(IsReschedule));
        await LoadCatalogAsync();
        SelectedCustomer = new CustomerListItem(item.CustomerId, 0, item.CustomerName, item.CustomerPhone, null, nameof(CustomerState.Active), null);
        CustomerSearch = item.CustomerName;
        SelectedService = Services.FirstOrDefault(s => s.Id == item.ServiceId);
        SelectedStaff = Staff.FirstOrDefault(s => s.Id == item.StaffMemberId);
        foreach (var r in Resources)
        {
            r.IsSelected = item.ResourceNames?.Split(", ").Contains(r.Resource.Name) == true;
        }

        Date = item.StartLocal.Date;
        Time = Formatting.Time(item.StartLocal);
        DurationMinutes = item.DurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Notes = item.Notes;
        BodyArea = item.BodyArea;
        _initializing = false;
        await LoadSlotsAsync();
    }

    partial void OnCustomerSearchChanged(string value)
    {
        if (_initializing || (SelectedCustomer is not null && value == SelectedCustomer.FullName))
        {
            return;
        }

        _ = _searchDebounce.RunAsync(async ct =>
        {
            var r = await _customers.SearchAsync(value, take: 8, ct: ct);
            CustomerResults.Clear();
            foreach (var c in r.OrEmpty())
            {
                CustomerResults.Add(c);
            }
        });
    }

    partial void OnSelectedCustomerChanged(CustomerListItem? value)
    {
        if (value is not null)
        {
            CustomerSearch = value.FullName;
            CustomerResults.Clear();
        }

        if (!_initializing)
        {
            _ = LoadPackagesAsync();
        }
    }

    partial void OnSelectedServiceChanged(ServiceDto? value)
    {
        if (value is not null && !IsReschedule)
        {
            DurationMinutes = value.DurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.RequiredResourceKind is { } kind && !Resources.Any(r => r.IsSelected && r.Resource.Kind == kind))
            {
                var first = Resources.FirstOrDefault(r => r.Resource.Kind == kind);
                if (first is not null)
                {
                    first.IsSelected = true;
                }
            }
        }

        if (!_initializing)
        {
            _ = OnSelectionChangedAsync();
        }
    }

    partial void OnSelectedStaffChanged(StaffDto? value)
    {
        if (!_initializing)
        {
            _ = LoadSlotsAsync();
        }
    }

    partial void OnDateChanged(DateTime value)
    {
        if (!_initializing)
        {
            _ = LoadSlotsAsync();
        }
    }

    [RelayCommand]
    private void PickSlot(SlotChoice slot) => Time = slot.Label;

    [RelayCommand]
    private Task ResourcesChangedAsync() => LoadSlotsAsync();

    [RelayCommand]
    private void StartAddCustomer()
    {
        IsAddingCustomer = true;
        var parts = CustomerSearch.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && !parts[0].Any(char.IsAsciiDigit))
        {
            NewFirstName = parts[0];
            NewLastName = parts.Length > 1 ? parts[1] : string.Empty;
        }
        else if (parts.Length > 0)
        {
            NewPhone = CustomerSearch.Trim();
        }
    }

    [RelayCommand]
    private void CancelAddCustomer() => IsAddingCustomer = false;

    [RelayCommand]
    private Task AddCustomerAsync() => RunAsync(async () =>
    {
        var details = new CustomerDetails(NewFirstName, NewLastName, NewPhone);
        var created = await _customers.CreateAsync(details, allowDuplicates: false);
        if (created.IsFailure && created.Error.Code == CustomerErrors.PossibleDuplicateCode)
        {
            if (!await _dialogs.ConfirmAsync("Benzer kayıt var", created.Error.Message + "\n\nYine de yeni müşteri olarak kaydedilsin mi?", "Yeni kayıt oluştur"))
            {
                return;
            }

            created = await _customers.CreateAsync(details, allowDuplicates: true);
        }

        if (!Check(created))
        {
            return;
        }

        var c = (await _customers.GetAsync(created.Value)).Value;
        SelectedCustomer = new CustomerListItem(c.Id, c.Number, c.FullName, c.Phone, c.Email, c.State, null);
        IsAddingCustomer = false;
        NewFirstName = NewLastName = NewPhone = string.Empty;
    });

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, false);

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (SelectedCustomer is null)
        {
            ErrorMessage = "Müşteri seçin veya yeni müşteri ekleyin.";
            return;
        }

        if (SelectedService is null || SelectedStaff is null)
        {
            ErrorMessage = "Hizmet ve personel seçin.";
            return;
        }

        if (Formatting.ParseTime(Time) is not { } time)
        {
            ErrorMessage = "Saati SS:DD biçiminde girin veya önerilen saatlerden birini seçin.";
            return;
        }

        int? duration = int.TryParse(DurationMinutes, out var d) && d != SelectedService.DurationMinutes ? d : null;
        var start = DateOnly.FromDateTime(Date).ToDateTime(time);
        var resources = Resources.Where(r => r.IsSelected).Select(r => r.Resource.Id).ToList();

        var outcome = await SubmitAsync(start, duration, resources, confirm: false);
        if (outcome is null)
        {
            return;
        }

        if (outcome.NeedsConfirmation)
        {
            var text = string.Join("\n", outcome.Issues.Select(i => "• " + i.Message));
            if (!await _dialogs.ConfirmAsync("Onay gerekiyor", text + "\n\nRandevu yine de kaydedilsin mi?", "Kaydet"))
            {
                return;
            }

            outcome = await SubmitAsync(start, duration, resources, confirm: true);
            if (outcome is null)
            {
                return;
            }
        }

        if (outcome.Saved)
        {
            Closed?.Invoke(this, true);
            return;
        }

        ErrorMessage = string.Join("\n", outcome.Issues.Select(i => i.Message));
    });

    private async Task<BookingOutcome?> SubmitAsync(DateTime start, int? duration, List<Guid> resources, bool confirm)
    {
        var result = _rescheduleId is { } id
            ? await _appointments.RescheduleAsync(new RescheduleRequest(id, start, SelectedStaff!.Id, resources, duration), confirm)
            : await _appointments.CreateAsync(new CreateAppointmentRequest(SelectedCustomer!.Id, SelectedService!.Id, SelectedStaff!.Id, resources, start, duration, Notes, BodyArea, SelectedPackage?.Id), confirm);
        return Check(result) ? result.Value : null;
    }

    private async Task LoadCatalogAsync()
    {
        var info = await _settings.GetBusinessInfoAsync();
        ShowBodyArea = info?.Features.Contains(ProfileFeature.BodyAreas) == true;
        Services.Clear();
        foreach (var s in (await _catalog.GetServicesAsync()).OrEmpty())
        {
            Services.Add(s);
        }

        Staff.Clear();
        foreach (var s in (await _catalog.GetStaffAsync()).OrEmpty())
        {
            Staff.Add(s);
        }

        Resources.Clear();
        foreach (var r in (await _catalog.GetResourcesAsync()).OrEmpty())
        {
            Resources.Add(new ResourceChoice(r));
        }

        SelectedStaff = Staff.FirstOrDefault();
    }

    private async Task OnSelectionChangedAsync()
    {
        if (SelectedService is not null && SelectedStaff is not null && SelectedStaff.ServiceIds.Count > 0 && !SelectedStaff.ServiceIds.Contains(SelectedService.Id))
        {
            var capable = Staff.FirstOrDefault(s => s.ServiceIds.Count == 0 || s.ServiceIds.Contains(SelectedService.Id));
            if (capable is not null)
            {
                _initializing = true;
                SelectedStaff = capable;
                _initializing = false;
            }
        }

        await LoadPackagesAsync();
        await LoadSlotsAsync();
    }

    private async Task LoadPackagesAsync()
    {
        Packages.Clear();
        SelectedPackage = null;
        if (SelectedCustomer is null || SelectedService is null || IsReschedule)
        {
            OnPropertyChanged(nameof(HasPackages));
            return;
        }

        var r = await _packages.GetCustomerPackagesAsync(SelectedCustomer.Id, activeOnly: true);
        foreach (var p in r.OrEmpty().Where(p => p.ServiceId == SelectedService.Id && p.RemainingSessions > 0))
        {
            Packages.Add(p);
        }

        SelectedPackage = Packages.FirstOrDefault();
        if (SelectedPackage?.BodyArea is { } area && string.IsNullOrWhiteSpace(BodyArea))
        {
            BodyArea = area;
        }

        OnPropertyChanged(nameof(HasPackages));
    }

    private async Task LoadSlotsAsync()
    {
        Slots.Clear();
        if (SelectedService is null || SelectedStaff is null)
        {
            return;
        }

        int? duration = int.TryParse(DurationMinutes, out var d) ? d : null;
        var r = await _appointments.GetAvailableSlotsAsync(new SlotQuery(
            DateOnly.FromDateTime(Date), SelectedService.Id, SelectedStaff.Id, Resources.Where(x => x.IsSelected).Select(x => x.Resource.Id).ToList(), duration, _rescheduleId));
        foreach (var s in r.OrEmpty())
        {
            Slots.Add(new SlotChoice(s.LocalStart));
        }

        NoSlots = Slots.Count == 0;
        if (string.IsNullOrEmpty(Time) && Slots.Count > 0)
        {
            Time = Slots[0].Label;
        }
    }
}
