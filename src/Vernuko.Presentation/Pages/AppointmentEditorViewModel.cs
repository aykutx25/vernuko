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

/// <param name="ResourceIds">Rooms/devices the time is free in; picking the slot selects them.</param>
/// <param name="ResourceHint">Shown when they differ from the current selection, e.g. "Lazer Odası 2".</param>
public sealed record SlotChoice(TimeOnly Time, IReadOnlyList<Guid>? ResourceIds = null, string? ResourceHint = null)
{
    public string TimeText => Time.ToString("HH:mm", TurkishText.Culture);

    public string Label => ResourceHint is null ? TimeText : $"{TimeText} · {ResourceHint}";
}

/// <summary>
/// Side panel for creating or moving an appointment without leaving the calendar. Keyboard-first: customer search,
/// then service, staff, room, date and time; free slots are suggested as the selection changes.
/// </summary>
public sealed partial class AppointmentEditorViewModel : ViewModelBase
{
    private readonly IAppointmentService _appointments;
    private readonly ICustomerService _customers;
    private readonly ICatalogService _catalog;
    private readonly IPackageService _packages;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly Debouncer _searchDebounce = new(TimeSpan.FromMilliseconds(250));
    private readonly Lock _listLock = new();
    private Guid? _rescheduleId;
    private int _slotsVersion;
    private int _packagesVersion;
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
    private string? _meetingUrl;

    /// <summary>Danışmanlık, psikoloji, eğitim: online görüşme bağlantısı.</summary>
    [ObservableProperty]
    private bool _showMeetingUrl;

    [ObservableProperty]
    private string _title = "Yeni Randevu";

    [ObservableProperty]
    private bool _noSlots;

    // Recurrence (create only) / apply-to-following (reschedule of a series member)
    [ObservableProperty]
    private bool _isRecurring;

    [ObservableProperty]
    private Option<RecurrenceFrequency>? _frequency;

    [ObservableProperty]
    private string _recurrenceInterval = "1";

    [ObservableProperty]
    private string _recurrenceCount = "8";

    [ObservableProperty]
    private bool _applyToFollowing;

    [ObservableProperty]
    private bool _isSeriesMember;

    [ObservableProperty]
    private bool _showRecurrence;

    public AppointmentEditorViewModel(IAppointmentService appointments, ICustomerService customers, ICatalogService catalog, IPackageService packages, ISettingsService settings, IDialogService dialogs)
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

    public bool HasResourceRule => SelectedService?.AllowedResourceIds is { Count: > 0 };

    /// <summary>e.g. "Lazer Epilasyon yalnızca şunlarla yapılır: Lazer Odası 1, Lazer Odası 2, Lazer Cihazı".</summary>
    public string? ResourceRuleText => SelectedService?.AllowedResourceIds is { Count: > 0 } allowed
        ? $"{SelectedService.Name} yalnızca şunlarla yapılır: {string.Join(", ", Resources.Where(r => allowed.Contains(r.Resource.Id)).Select(r => r.Resource.Name))}"
        : null;

    public IReadOnlyList<Option<RecurrenceFrequency>> FrequencyOptions { get; } =
    [
        new("Gün", RecurrenceFrequency.Daily), new("Hafta", RecurrenceFrequency.Weekly), new("Ay", RecurrenceFrequency.Monthly),
    ];

    public string RecurrenceSummary => TryPattern() is { } p ? p.Describe() : "Aralık ve tekrar sayısını kontrol edin.";

    partial void OnIsRecurringChanged(bool value) => OnPropertyChanged(nameof(RecurrenceSummary));

    partial void OnFrequencyChanged(Option<RecurrenceFrequency>? value) => OnPropertyChanged(nameof(RecurrenceSummary));

    partial void OnRecurrenceIntervalChanged(string value) => OnPropertyChanged(nameof(RecurrenceSummary));

    partial void OnRecurrenceCountChanged(string value) => OnPropertyChanged(nameof(RecurrenceSummary));

    private RecurrencePattern? TryPattern() =>
        int.TryParse(RecurrenceInterval, out var interval) && int.TryParse(RecurrenceCount, out var count) &&
        new RecurrencePattern(Frequency?.Value ?? RecurrenceFrequency.Weekly, interval, count) is var p && p.Validate().IsSuccess
            ? p
            : null;

    public async Task InitializeForCreateAsync(DateOnly date, Guid? customerId)
    {
        _initializing = true;
        await LoadCatalogAsync();
        Frequency = FrequencyOptions[1];
        ShowRecurrence = true; // generic engine feature, offered in every profile (e.g. laser every 6 weeks)
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
        IsSeriesMember = item.IsInSeries;
        ShowRecurrence = false;
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
        OnPropertyChanged(nameof(HasResourceRule));
        OnPropertyChanged(nameof(ResourceRuleText));
        if (value is not null && !IsReschedule)
        {
            DurationMinutes = value.DurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.AllowedResourceIds is { Count: > 0 } allowed)
            {
                SelectAllowedResources(allowed);
            }
            else if (value.RequiredResourceKind is { } kind && !Resources.Any(r => r.IsSelected && r.Resource.Kind == kind))
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

    /// <summary>
    /// The service may only use some rooms/devices (laser → laser rooms + laser device): drop disallowed ones of
    /// those kinds and pick the first allowed one where none is selected. Other kinds are left alone.
    /// </summary>
    private void SelectAllowedResources(IReadOnlyList<Guid> allowed)
    {
        var allowedChoices = Resources.Where(r => allowed.Contains(r.Resource.Id)).ToList();
        foreach (var kind in allowedChoices.Select(r => r.Resource.Kind).Distinct())
        {
            foreach (var r in Resources.Where(r => r.Resource.Kind == kind && r.IsSelected && !allowed.Contains(r.Resource.Id)))
            {
                r.IsSelected = false;
            }

            if (!allowedChoices.Any(r => r.Resource.Kind == kind && r.IsSelected))
            {
                allowedChoices.First(r => r.Resource.Kind == kind).IsSelected = true;
            }
        }
    }

    [RelayCommand]
    private async Task PickSlotAsync(SlotChoice slot)
    {
        Time = slot.TimeText;
        if (slot.ResourceIds is not { } ids || SelectedService?.AllowedResourceIds is not { Count: > 0 } allowed)
        {
            return;
        }

        // Switch to the rooms/devices this time is free in (only the kinds the service is limited to).
        var kinds = Resources.Where(r => allowed.Contains(r.Resource.Id)).Select(r => r.Resource.Kind).Distinct().ToList();
        var changed = false;
        foreach (var r in Resources.Where(r => kinds.Contains(r.Resource.Kind)))
        {
            var select = ids.Contains(r.Resource.Id);
            changed |= r.IsSelected != select;
            r.IsSelected = select;
        }

        if (changed)
        {
            await LoadSlotsAsync();
        }
    }

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

        if (!IsReschedule && IsRecurring)
        {
            await SaveSeriesAsync(start, duration, resources);
            return;
        }

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

    /// <summary>
    /// Creates a recurring series. Dates with a hard conflict are listed and can be skipped explicitly; warnings
    /// (e.g. outside working hours, package overbooked) need confirmation. Nothing is saved silently.
    /// </summary>
    private async Task SaveSeriesAsync(DateTime start, int? duration, List<Guid> resources)
    {
        if (TryPattern() is not { } pattern)
        {
            ErrorMessage = $"Tekrar aralığı 1–12, tekrar sayısı 2–{RecurrencePattern.MaxCount} olmalıdır.";
            return;
        }

        var request = new CreateAppointmentRequest(SelectedCustomer!.Id, SelectedService!.Id, SelectedStaff!.Id, resources, start, duration, Notes, BodyArea, SelectedPackage?.Id, MeetingUrl: MeetingUrl);
        var first = await _appointments.CreateSeriesAsync(request, pattern, confirmWarnings: false, skipBlocked: false);
        if (!Check(first))
        {
            return;
        }

        var outcome = first.Value;
        if (!outcome.Saved)
        {
            var lines = outcome.Problems.Select(p => $"• {Formatting.ShortDate(p.StartLocal)} {Formatting.Time(p.StartLocal)}: {string.Join("; ", p.Issues.Select(i => i.Message))}");
            var blocking = outcome.Problems.Count(p => p.Issues.Any(i => i.Severity == IssueSeverity.Blocking));
            var question = blocking > 0
                ? $"\n\n{blocking} tarihte randevu verilemiyor; bu tarihler ATLANACAK. Diğer tarihler kaydedilsin mi?"
                : "\n\nSeri yine de kaydedilsin mi?";
            if (!await _dialogs.ConfirmAsync("Tekrarlayan randevu", string.Join("\n", lines) + question, "Kaydet"))
            {
                return;
            }

            var second = await _appointments.CreateSeriesAsync(request, pattern, confirmWarnings: true, skipBlocked: true);
            if (!Check(second))
            {
                return;
            }

            outcome = second.Value;
        }

        if (outcome.Saved)
        {
            await _dialogs.ShowMessageAsync("Tekrarlayan randevu", $"{outcome.CreatedLocal.Count} randevu oluşturuldu ({pattern.Describe()}).");
            Closed?.Invoke(this, true);
        }
    }

    private async Task<BookingOutcome?> SubmitAsync(DateTime start, int? duration, List<Guid> resources, bool confirm)
    {
        var result = _rescheduleId is { } id
            ? await _appointments.RescheduleAsync(new RescheduleRequest(id, start, SelectedStaff!.Id, resources, duration, IsSeriesMember && ApplyToFollowing), confirm)
            : await _appointments.CreateAsync(new CreateAppointmentRequest(SelectedCustomer!.Id, SelectedService!.Id, SelectedStaff!.Id, resources, start, duration, Notes, BodyArea, SelectedPackage?.Id, MeetingUrl: MeetingUrl), confirm);
        return Check(result) ? result.Value : null;
    }

    private async Task LoadCatalogAsync()
    {
        var info = await _settings.GetBusinessInfoAsync();
        ShowBodyArea = info?.Features.Contains(ProfileFeature.BodyAreas) == true;
        ShowMeetingUrl = info?.Features.Contains(ProfileFeature.OnlineMeetingLinks) == true;
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
        // Several changes can start a reload at once (customer, service); only the latest one is shown.
        var version = Interlocked.Increment(ref _packagesVersion);
        var customer = SelectedCustomer;
        var service = SelectedService;
        IReadOnlyList<CustomerPackageDto> found = [];
        if (customer is not null && service is not null && !IsReschedule)
        {
            var r = await _packages.GetCustomerPackagesAsync(customer.Id, activeOnly: true);
            found = r.OrEmpty().Where(p => p.ServiceId == service.Id && p.RemainingSessions > 0).ToList();
        }

        lock (_listLock)
        {
            if (version != Volatile.Read(ref _packagesVersion))
            {
                return;
            }

            Packages.Clear();
            foreach (var p in found)
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
    }

    private async Task LoadSlotsAsync()
    {
        // Selection changes and explicit refreshes can overlap (slowly over the network on a connected computer);
        // results are applied in one step and only for the latest request, so an older answer never wins.
        var version = Interlocked.Increment(ref _slotsVersion);
        var service = SelectedService;
        var staff = SelectedStaff;
        var selected = Resources.Where(x => x.IsSelected).Select(x => x.Resource.Id).ToList();
        IReadOnlyList<SlotChoice> found = [];
        if (service is not null && staff is not null)
        {
            int? duration = int.TryParse(DurationMinutes, out var d) ? d : null;
            var r = await _appointments.GetAvailableSlotsAsync(new SlotQuery(
                DateOnly.FromDateTime(Date), service.Id, staff.Id, selected, duration, _rescheduleId,
                SearchAllowedResources: service.AllowedResourceIds is { Count: > 0 }));
            var names = Resources.ToDictionary(x => x.Resource.Id, x => x.Resource.Name);
            found = r.OrEmpty().Select(s =>
            {
                var other = s.ResourceIds.Where(id => !selected.Contains(id)).ToList();
                var hint = other.Count == 0 ? null : string.Join(", ", other.Select(id => names.GetValueOrDefault(id, "?")));
                return new SlotChoice(s.LocalStart, s.ResourceIds, hint);
            }).ToList();
        }

        lock (_listLock)
        {
            if (version != Volatile.Read(ref _slotsVersion))
            {
                return;
            }

            Slots.Clear();
            foreach (var slot in found)
            {
                Slots.Add(slot);
            }

            NoSlots = Slots.Count == 0;
            if (string.IsNullOrEmpty(Time) && Slots.Count > 0)
            {
                // Only auto-fill a time that fits the current selection; a different room needs a deliberate click.
                Time = Slots.FirstOrDefault(s => s.ResourceHint is null)?.TimeText ?? string.Empty;
            }
        }
    }
}
