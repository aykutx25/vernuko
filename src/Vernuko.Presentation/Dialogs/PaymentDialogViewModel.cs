using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Dialogs;

/// <summary>"Ödeme Al" overlay. One idempotency key per dialog instance: double clicks never double-record.</summary>
public sealed partial class PaymentDialogViewModel(PaymentService payments) : ViewModelBase
{
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");
    private Guid _customerId;
    private Guid? _appointmentId;
    private Guid? _packageId;
    private bool _saved;

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private string _amount = string.Empty;

    [ObservableProperty]
    private Option<PaymentMethod> _method = Methods[0];

    [ObservableProperty]
    private bool _isDeposit;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string _balanceText = string.Empty;

    public static IReadOnlyList<Option<PaymentMethod>> Methods { get; } =
        Enum.GetValues<PaymentMethod>().Select(m => new Option<PaymentMethod>(CustomerTransaction.MethodName(m), m)).ToList();

    public event EventHandler<bool>? Closed;

    public void Initialize(Guid customerId, string customerName, Guid? appointmentId, long suggestedMinor, Guid? packageId = null)
    {
        _customerId = customerId;
        _appointmentId = appointmentId;
        _packageId = packageId;
        CustomerName = customerName;
        Amount = suggestedMinor > 0 ? (suggestedMinor / 100m).ToString("0.##", TurkishText.Culture) : string.Empty;
        _ = LoadBalanceAsync();
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_saved)
        {
            return;
        }

        var money = Money.Parse(Amount);
        if (!Check(money))
        {
            return;
        }

        var r = await payments.RecordAsync(new RecordTransactionRequest(_customerId, TransactionKind.Payment, money.Value.Minor, Method.Value, IsDeposit, _appointmentId, _packageId, Description, _idempotencyKey));
        if (Check(r))
        {
            _saved = true;
            Closed?.Invoke(this, true);
        }
    });

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, false);

    private async Task LoadBalanceAsync()
    {
        var b = await payments.GetBalanceAsync(_customerId);
        BalanceText = b.IsFailure ? string.Empty : b.Value > 0 ? $"Açık bakiye: {Formatting.Money(b.Value)}" : b.Value < 0 ? $"Alacak (ön ödeme): {Formatting.Money(-b.Value)}" : "Açık bakiye yok";
    }
}

/// <summary>"Paket Satışı" overlay.</summary>
public sealed partial class SellPackageDialogViewModel(PackageService packages, CatalogService catalog, CustomerService customers) : ViewModelBase
{
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");
    private bool _saved;

    [ObservableProperty]
    private CustomerListItem? _customer;

    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private PackageTemplateDto? _template;

    [ObservableProperty]
    private ServiceDto? _service;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _bodyArea;

    [ObservableProperty]
    private string _sessions = "8";

    [ObservableProperty]
    private string _price = string.Empty;

    [ObservableProperty]
    private string _validityDays = "365";

    [ObservableProperty]
    private string _initialPayment = "0";

    [ObservableProperty]
    private Option<PaymentMethod> _method = PaymentDialogViewModel.Methods[0];

    public System.Collections.ObjectModel.ObservableCollection<PackageTemplateDto> Templates { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<ServiceDto> Services { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public static IReadOnlyList<Option<PaymentMethod>> Methods => PaymentDialogViewModel.Methods;

    public event EventHandler<bool>? Closed;

    public async Task InitializeAsync(Guid? customerId)
    {
        foreach (var t in (await catalog.GetPackageTemplatesAsync()).OrEmpty())
        {
            Templates.Add(t);
        }

        foreach (var s in (await catalog.GetServicesAsync()).OrEmpty())
        {
            Services.Add(s);
        }

        if (customerId is { } id && (await customers.GetAsync(id)) is { IsSuccess: true } c)
        {
            Customer = new CustomerListItem(c.Value.Id, c.Value.Number, c.Value.FullName, c.Value.Phone, c.Value.Email, c.Value.State, null);
            CustomerSearch = c.Value.FullName;
        }

        Template = Templates.FirstOrDefault();
    }

    partial void OnTemplateChanged(PackageTemplateDto? value)
    {
        if (value is null)
        {
            return;
        }

        Service = Services.FirstOrDefault(s => s.Id == value.ServiceId);
        Name = value.Name;
        Sessions = value.SessionCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Price = (value.PriceMinor / 100m).ToString("0.##", TurkishText.Culture);
        ValidityDays = value.ValidityDays?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    partial void OnCustomerSearchChanged(string value)
    {
        if (Customer is not null && Customer.FullName == value)
        {
            return;
        }

        _ = SearchAsync(value);
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
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_saved)
        {
            return;
        }

        if (Customer is null || Service is null)
        {
            ErrorMessage = "Müşteri ve hizmet seçin.";
            return;
        }

        var price = Money.Parse(Price);
        var paid = Money.Parse(string.IsNullOrWhiteSpace(InitialPayment) ? "0" : InitialPayment);
        if (!Check(price) || !Check(paid))
        {
            return;
        }

        if (!int.TryParse(Sessions, out var sessions))
        {
            ErrorMessage = "Seans sayısı geçersiz.";
            return;
        }

        int? validity = int.TryParse(ValidityDays, out var v) ? v : null;
        var r = await packages.SellAsync(new SellPackageRequest(Customer.Id, Service.Id, Template?.Id, Name, BodyArea, sessions, price.Value.Minor, validity, paid.Value.Minor, paid.Value.Minor > 0 ? Method.Value : null, _idempotencyKey));
        if (Check(r))
        {
            _saved = true;
            Closed?.Invoke(this, true);
        }
    });

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, false);

    private async Task SearchAsync(string text)
    {
        var r = await customers.SearchAsync(text, take: 6);
        CustomerResults.Clear();
        foreach (var c in r.OrEmpty())
        {
            CustomerResults.Add(c);
        }
    }
}
