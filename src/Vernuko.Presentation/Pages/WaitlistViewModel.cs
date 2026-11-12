using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

/// <summary>Bekleme listesi: customers who want an earlier/free slot.</summary>
public sealed partial class WaitlistViewModel(IWaitlistService waitlist, ICustomerService customers, ICatalogService catalog, IDialogService dialogs, TimeProvider time) : PageViewModel, IShellAware
{
    private readonly Debouncer _debounce = new(TimeSpan.FromMilliseconds(250));

    [ObservableProperty]
    private bool _includeClosed;

    [ObservableProperty]
    private WaitlistItem? _selected;

    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerListItem? _customer;

    [ObservableProperty]
    private Option<Guid?>? _service;

    [ObservableProperty]
    private Option<Guid?>? _staff;

    [ObservableProperty]
    private DateTime _from = time.GetLocalNow().Date;

    [ObservableProperty]
    private DateTime _to = time.GetLocalNow().Date.AddDays(14);

    [ObservableProperty]
    private string? _preferredTime;

    [ObservableProperty]
    private string? _notes;

    public override string Title => "Bekleme Listesi";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<WaitlistItem> Items { get; } = [];

    public ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public ObservableCollection<Option<Guid?>> ServiceOptions { get; } = [];

    public ObservableCollection<Option<Guid?>> StaffOptions { get; } = [];

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        ServiceOptions.Clear();
        ServiceOptions.Add(new Option<Guid?>("Herhangi bir hizmet", null));
        foreach (var s in (await catalog.GetServicesAsync()).OrEmpty())
        {
            ServiceOptions.Add(new Option<Guid?>(s.Name, s.Id));
        }

        StaffOptions.Clear();
        StaffOptions.Add(new Option<Guid?>("Herhangi bir personel", null));
        foreach (var s in (await catalog.GetStaffAsync()).OrEmpty())
        {
            StaffOptions.Add(new Option<Guid?>(s.DisplayName, s.Id));
        }

        Service ??= ServiceOptions[0];
        Staff ??= StaffOptions[0];
        await LoadAsync();
    });

    partial void OnIncludeClosedChanged(bool value) => _ = LoadAsync();

    partial void OnCustomerSearchChanged(string value)
    {
        if (Customer?.FullName == value)
        {
            return;
        }

        _ = _debounce.RunAsync(async _ =>
        {
            CustomerResults.Clear();
            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (var c in (await customers.SearchAsync(value, take: 6)).OrEmpty())
                {
                    CustomerResults.Add(c);
                }
            }
        });
    }

    partial void OnCustomerChanged(CustomerListItem? value)
    {
        if (value is not null)
        {
            CustomerSearch = value.FullName;
            CustomerResults.Clear();
        }
    }

    [RelayCommand]
    private Task AddAsync() => RunAsync(async () =>
    {
        if (Customer is null)
        {
            var (resolved, error) = await ResultExtensions.ResolveTypedCustomerAsync(customers, CustomerSearch);
            if (resolved is null)
            {
                ErrorMessage = error;
                return;
            }

            Customer = resolved;
        }

        var r = await waitlist.AddAsync(Customer.Id, Service?.Value, Staff?.Value, DateOnly.FromDateTime(From), DateOnly.FromDateTime(To), PreferredTime, Notes);
        if (Check(r))
        {
            Customer = null;
            CustomerSearch = string.Empty;
            PreferredTime = null;
            Notes = null;
            await LoadAsync();
            InfoMessage = "Bekleme listesine eklendi. Uygun bir randevu iptal edildiğinde size hatırlatılacak.";
        }
    });

    [RelayCommand]
    private Task BookAsync(WaitlistItem item) => RunAsync(async () =>
    {
        await waitlist.SetStatusAsync(item.Id, WaitlistStatus.Contacted);
        if (Shell is not null)
        {
            await Shell.NavigateAsync("appointments", new OpenNewAppointment(item.CustomerId));
        }
    });

    [RelayCommand]
    private Task MarkContactedAsync(WaitlistItem item) => SetAsync(item, WaitlistStatus.Contacted);

    [RelayCommand]
    private Task MarkBookedAsync(WaitlistItem item) => SetAsync(item, WaitlistStatus.Booked);

    [RelayCommand]
    private async Task CancelEntryAsync(WaitlistItem item)
    {
        if (await dialogs.ConfirmAsync("Bekleme kaydını kapat", $"{item.CustomerName} bekleme listesinden çıkarılsın mı?", "Çıkar"))
        {
            await SetAsync(item, WaitlistStatus.Cancelled);
        }
    }

    private Task SetAsync(WaitlistItem item, WaitlistStatus status) => RunAsync(async () =>
    {
        if (Check(await waitlist.SetStatusAsync(item.Id, status)))
        {
            await LoadAsync();
        }
    });

    private async Task LoadAsync()
    {
        Items.Clear();
        foreach (var i in (await waitlist.ListAsync(IncludeClosed)).OrEmpty())
        {
            Items.Add(i);
        }
    }
}
