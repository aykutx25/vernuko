using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Customers;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed partial class CustomersViewModel : PageViewModel, IShellAware
{
    private readonly ICustomerService _customers;
    private readonly IPackageService _packages;
    private readonly IPaymentService _payments;
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogs;
    private readonly IPlatformServices _platform;
    private readonly Debouncer _debounce = new(TimeSpan.FromMilliseconds(250));

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _includeArchived;

    [ObservableProperty]
    private CustomerListItem? _selected;

    [ObservableProperty]
    private CustomerDetailsDto? _details;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isNew;

    // Editable form fields
    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private DateTime? _birthDate;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _nationalId;

    [ObservableProperty]
    private string? _companyName;

    [ObservableProperty]
    private string? _taxOffice;

    [ObservableProperty]
    private string? _taxNumber;

    [ObservableProperty]
    private bool _allowSms = true;

    [ObservableProperty]
    private bool _allowWhatsApp = true;

    [ObservableProperty]
    private bool _allowEmail = true;

    [ObservableProperty]
    private bool _marketingConsent;

    [ObservableProperty]
    private string _balanceText = string.Empty;

    public CustomersViewModel(ICustomerService customers, IPackageService packages, IPaymentService payments, IServiceProvider services, IDialogService dialogs, IPlatformServices platform, CustomerRecordsViewModel records, PrivateNotesViewModel privateNotes)
    {
        Records = records;
        PrivateNotes = privateNotes;
        _customers = customers;
        _packages = packages;
        _payments = payments;
        _services = services;
        _dialogs = dialogs;
        _platform = platform;
    }

    public override string Title => "Müşteriler";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<CustomerListItem> Results { get; } = [];

    public ObservableCollection<CustomerPackageDto> CustomerPackages { get; } = [];

    public ObservableCollection<AppointmentListItem> History { get; } = [];

    public ObservableCollection<TransactionDto> Transactions { get; } = [];

    /// <summary>Consents and encrypted files of the selected customer.</summary>
    public CustomerRecordsViewModel Records { get; }

    /// <summary>Gizli uzman notları (psikoloji, danışmanlık, hukuk profilleri; yalnızca yazarı görür).</summary>
    public PrivateNotesViewModel PrivateNotes { get; }

    public bool ShowPrivateNotes => PrivateNotes.CanUse && Shell?.Business?.Features.Contains(BusinessProfiles.ProfileFeature.PrivateNotes) == true;

    public bool HasSelection => Details is not null;

    public bool CanSeeSensitive => Shell?.Has(Permission.CustomerSensitiveDataView) ?? false;

    public bool CanSeePayments => Shell?.Has(Permission.PaymentsView) ?? false;

    /// <summary>Firma bilgileri (unvan, vergi dairesi, VKN) — consulting/accounting/law profiles, or when already filled.</summary>
    public bool ShowCompany => Shell?.Business?.Features.Contains(BusinessProfiles.ProfileFeature.CompanyClients) == true || Details?.CompanyLine is not null;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        switch (parameter)
        {
            case CustomerSearchRequest s:
                SearchText = s.Query;
                await SearchAsync();
                break;
            case OpenCustomer o:
                await SearchAsync();
                await LoadDetailsAsync(o.CustomerId);
                break;
            default:
                await SearchAsync();
                break;
        }
    }

    partial void OnSearchTextChanged(string value) => _ = _debounce.RunAsync(_ => SearchAsync());

    partial void OnIncludeArchivedChanged(bool value) => _ = SearchAsync();

    partial void OnSelectedChanged(CustomerListItem? value)
    {
        if (value is not null)
        {
            _ = LoadDetailsAsync(value.Id);
        }
    }

    partial void OnDetailsChanged(CustomerDetailsDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowCompany));
    }

    [RelayCommand]
    private Task SearchAsync() => RunAsync(async () =>
    {
        var r = await _customers.SearchAsync(SearchText, IncludeArchived, 200);
        if (!Check(r))
        {
            return;
        }

        Results.Clear();
        foreach (var c in r.Value)
        {
            Results.Add(c);
        }
    });

    [RelayCommand]
    private void New()
    {
        Selected = null;
        Details = null;
        IsNew = true;
        IsEditing = true;
        FirstName = LastName = string.Empty;
        Phone = Email = Address = Notes = NationalId = CompanyName = TaxOffice = TaxNumber = null;
        BirthDate = null;
        AllowSms = AllowWhatsApp = AllowEmail = true;
        MarketingConsent = false;
        var parts = SearchText.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && !parts[0].Any(char.IsAsciiDigit))
        {
            FirstName = parts[0];
            LastName = parts.Length > 1 ? parts[1] : string.Empty;
        }
    }

    [RelayCommand]
    private void Edit()
    {
        if (Details is null)
        {
            return;
        }

        IsNew = false;
        IsEditing = true;
        FirstName = Details.FirstName;
        LastName = Details.LastName;
        Phone = Details.Phone;
        Email = Details.Email;
        BirthDate = Details.BirthDate?.ToDateTime(TimeOnly.MinValue);
        Address = Details.Address;
        Notes = Details.Notes;
        NationalId = CanSeeSensitive ? Details.NationalIdMasked : null;
        CompanyName = Details.CompanyName;
        TaxOffice = Details.TaxOffice;
        TaxNumber = Details.TaxNumber;
        AllowSms = Details.AllowSms;
        AllowWhatsApp = Details.AllowWhatsApp;
        AllowEmail = Details.AllowEmail;
        MarketingConsent = Details.MarketingConsent;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsNew = false;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var details = new CustomerDetails(FirstName, LastName, Phone, Email, BirthDate is { } b ? DateOnly.FromDateTime(b) : null, Address, Notes, CanSeeSensitive ? NationalId : null, CompanyName, TaxOffice, TaxNumber);
        Guid id;
        if (IsNew)
        {
            var created = await _customers.CreateAsync(details, allowDuplicates: false);
            if (created.IsFailure && created.Error.Code == CustomerErrors.PossibleDuplicateCode)
            {
                if (!await _dialogs.ConfirmAsync("Benzer kayıt var", created.Error.Message + "\n\nFarklı bir kişi olarak kaydedilsin mi?", "Yeni kayıt oluştur"))
                {
                    return;
                }

                created = await _customers.CreateAsync(details, allowDuplicates: true);
            }

            if (!Check(created))
            {
                return;
            }

            id = created.Value;
        }
        else
        {
            id = Details!.Id;
            var updated = await _customers.UpdateAsync(id, Details.Version, details, AllowSms, AllowWhatsApp, AllowEmail, MarketingConsent);
            if (!Check(updated))
            {
                return;
            }
        }

        IsEditing = false;
        IsNew = false;
        await SearchAsync();
        await LoadDetailsAsync(id);
        InfoMessage = "Müşteri kaydedildi.";
    });

    [RelayCommand]
    private Task ArchiveAsync() => RunAsync(async () =>
    {
        if (Details is null || !await _dialogs.ConfirmAsync("Arşivle", $"{Details.FullName} arşive taşınsın mı? Kayıtları silinmez, aramada gizlenir.", "Arşivle"))
        {
            return;
        }

        if (Check(await _customers.ArchiveAsync(Details.Id)))
        {
            await LoadDetailsAsync(Details.Id);
            await SearchAsync();
        }
    });

    [RelayCommand]
    private Task RestoreAsync() => RunAsync(async () =>
    {
        if (Details is not null && Check(await _customers.RestoreAsync(Details.Id)))
        {
            await LoadDetailsAsync(Details.Id);
            await SearchAsync();
        }
    });

    [RelayCommand]
    private Task AnonymizeAsync() => RunAsync(async () =>
    {
        if (Details is null)
        {
            return;
        }

        var reason = await _dialogs.PromptAsync("Kişisel verileri sil (anonimleştir)",
            $"{Details.FullName} kaydındaki ad, telefon, e-posta, adres, notlar ve kimlik bilgisi KALICI olarak silinecek. Gelecekteki randevuları iptal edilecek; finansal kayıtlar kimliksiz olarak kalır. Bu işlem geri alınamaz.\n\nGerekçe (ör. KVKK silme talebi):");
        if (reason is null || !await _dialogs.ConfirmAsync("Emin misiniz?", "Bu işlem geri alınamaz.", "Kalıcı olarak anonimleştir", destructive: true))
        {
            return;
        }

        if (Check(await _customers.AnonymizeAsync(Details.Id, reason)))
        {
            await LoadDetailsAsync(Details.Id);
            await SearchAsync();
        }
    });

    [RelayCommand]
    private Task ExportCustomerAsync() => RunAsync(async () =>
    {
        if (Details is null)
        {
            return;
        }

        var path = await _dialogs.PickSaveFileAsync("Müşteri verilerini dışa aktar", $"musteri-{Details.Number}.json", "JSON dosyası|*.json");
        if (path is null)
        {
            return;
        }

        var json = await _customers.ExportCustomerJsonAsync(Details.Id);
        if (Check(json))
        {
            await _platform.WriteTextFileAsync(path, json.Value);
            InfoMessage = "Müşteri verileri dışa aktarıldı.";
        }
    });

    [RelayCommand]
    private Task ExportAllAsync() => RunAsync(async () =>
    {
        var path = await _dialogs.PickSaveFileAsync("Müşteri listesini dışa aktar", $"musteriler-{DateTime.Today:yyyyMMdd}.csv", "CSV dosyası|*.csv");
        if (path is null)
        {
            return;
        }

        var csv = await _customers.ExportAllCsvAsync();
        if (Check(csv))
        {
            await _platform.WriteTextFileAsync(path, csv.Value);
            InfoMessage = "Müşteri listesi dışa aktarıldı (Excel ile açılabilir).";
        }
    });

    [RelayCommand]
    private Task NewAppointmentAsync() => Details is null ? Task.CompletedTask : Shell?.NavigateAsync("appointments", new OpenNewAppointment(Details.Id)) ?? Task.CompletedTask;

    [RelayCommand]
    private async Task SellPackageAsync()
    {
        if (Details is null || Shell is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<SellPackageDialogViewModel>();
        var id = Details.Id;
        dialog.Closed += async (_, saved) =>
        {
            Shell.CloseOverlay();
            if (saved)
            {
                await LoadDetailsAsync(id);
                InfoMessage = "Paket satışı kaydedildi.";
            }
        };
        await dialog.InitializeAsync(id);
        Shell.ShowOverlay(dialog);
    }

    [RelayCommand]
    private void TakePayment()
    {
        if (Details is null || Shell is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<PaymentDialogViewModel>();
        var id = Details.Id;
        dialog.Closed += async (_, saved) =>
        {
            Shell.CloseOverlay();
            if (saved)
            {
                await LoadDetailsAsync(id);
                InfoMessage = "Ödeme kaydedildi.";
            }
        };
        dialog.Initialize(id, Details.FullName, null, Math.Max(0, Details.BalanceMinor));
        Shell.ShowOverlay(dialog);
    }

    public async Task LoadDetailsAsync(Guid id)
    {
        var d = await _customers.GetAsync(id);
        if (!Check(d))
        {
            return;
        }

        Details = d.Value;
        IsEditing = false;
        BalanceText = d.Value.BalanceMinor switch
        {
            > 0 => $"Açık bakiye: {Formatting.Money(d.Value.BalanceMinor)}",
            < 0 => $"Ön ödeme: {Formatting.Money(-d.Value.BalanceMinor)}",
            _ => "Bakiye yok",
        };

        CustomerPackages.Clear();
        foreach (var p in (await _packages.GetCustomerPackagesAsync(id)).OrEmpty())
        {
            CustomerPackages.Add(p);
        }

        History.Clear();
        foreach (var h in (await _customers.GetHistoryAsync(id)).OrEmpty())
        {
            History.Add(h);
        }

        Transactions.Clear();
        if (CanSeePayments)
        {
            foreach (var t in (await _payments.GetCustomerTransactionsAsync(id)).OrEmpty())
            {
                Transactions.Add(t);
            }
        }

        await Records.LoadAsync(id);
        if (ShowPrivateNotes)
        {
            await PrivateNotes.LoadAsync(id);
        }
    }
}
