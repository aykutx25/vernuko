using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed partial class MaterialRow : ObservableObject
{
    [ObservableProperty]
    private ProductDto? _product;

    [ObservableProperty]
    private string _quantity = "1";
}

/// <summary>Stok: products, stock in/out, automatic service materials and product sales.</summary>
public sealed partial class InventoryViewModel(IInventoryService inventory, ICatalogService catalog, ICustomerService customers, IDialogService dialogs) : PageViewModel, IShellAware
{
    private readonly Debouncer _debounce = new(TimeSpan.FromMilliseconds(250));
    private string _saleKey = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private ProductDto? _selected;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _sku;

    [ObservableProperty]
    private string _unit = "adet";

    [ObservableProperty]
    private string _salePrice = "0";

    [ObservableProperty]
    private string _cost = "0";

    [ObservableProperty]
    private string _minimumStock = "0";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _lowOnly;

    // Sale form
    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerListItem? _saleCustomer;

    [ObservableProperty]
    private string _saleQuantity = "1";

    [ObservableProperty]
    private string _salePriceText = "0";

    [ObservableProperty]
    private StaffDto? _saleStaff;

    [ObservableProperty]
    private Option<PaymentMethod?>? _salePayment;

    // Materials
    [ObservableProperty]
    private ServiceDto? _materialService;

    public override string Title => "Stok";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<ProductDto> Products { get; } = [];

    public ObservableCollection<StockMovementDto> Movements { get; } = [];

    public ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public ObservableCollection<StaffDto> Staff { get; } = [];

    public ObservableCollection<ServiceDto> Services { get; } = [];

    public ObservableCollection<MaterialRow> Materials { get; } = [];

    public IReadOnlyList<Option<PaymentMethod?>> PaymentOptions { get; } =
        new[] { new Option<PaymentMethod?>("Daha sonra ödenecek (borç)", null) }
            .Concat(PaymentDialogViewModel.MoneyMethods.Select(m => new Option<PaymentMethod?>("Hemen tahsil et: " + m.Label, m.Value))).ToList();

    public bool CanManage => Shell?.Has(Permission.InventoryManage) ?? false;

    public bool CanSell => Shell?.Has(Permission.ProductsSell) ?? false;

    public int LowCount => Products.Count(p => p.IsLow);

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        Staff.Clear();
        foreach (var s in (await catalog.GetStaffAsync()).OrEmpty())
        {
            Staff.Add(s);
        }

        Services.Clear();
        foreach (var s in (await catalog.GetServicesAsync()).OrEmpty())
        {
            Services.Add(s);
        }

        SalePayment ??= PaymentOptions[0];
        await LoadAsync();
        OnPropertyChanged(nameof(CanManage));
        OnPropertyChanged(nameof(CanSell));
    });

    partial void OnSelectedChanged(ProductDto? value)
    {
        if (value is null)
        {
            return;
        }

        Name = value.Name;
        Sku = value.Sku;
        Unit = value.Unit;
        SalePrice = (value.SalePriceMinor / 100m).ToString("0.##", TurkishText.Culture);
        Cost = (value.CostMinor / 100m).ToString("0.##", TurkishText.Culture);
        MinimumStock = value.MinimumStock.ToString(CultureInfo.InvariantCulture);
        IsActive = value.IsActive;
        SalePriceText = SalePrice;
        _ = LoadMovementsAsync();
    }

    partial void OnLowOnlyChanged(bool value) => _ = LoadAsync();

    partial void OnMaterialServiceChanged(ServiceDto? value) => _ = LoadMaterialsAsync();

    partial void OnCustomerSearchChanged(string value)
    {
        if (SaleCustomer?.FullName == value)
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

    partial void OnSaleCustomerChanged(CustomerListItem? value)
    {
        if (value is not null)
        {
            CustomerSearch = value.FullName;
            CustomerResults.Clear();
        }
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        Name = string.Empty;
        Sku = null;
        Unit = "adet";
        SalePrice = "0";
        Cost = "0";
        MinimumStock = "0";
        IsActive = true;
        Movements.Clear();
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var price = Money.Parse(SalePrice);
        var cost = Money.Parse(Cost);
        if (!Check(price) || !Check(cost))
        {
            return;
        }

        if (!int.TryParse(MinimumStock, out var min))
        {
            ErrorMessage = "Kritik stok seviyesi tam sayı olmalıdır.";
            return;
        }

        var r = await inventory.SaveProductAsync(new ProductDto(Selected?.Id ?? Guid.Empty, Name, Sku, Unit, price.Value.Minor, cost.Value.Minor, min, IsActive, 0));
        if (Check(r))
        {
            await LoadAsync();
            Selected = Products.FirstOrDefault(p => p.Id == r.Value);
            InfoMessage = "Ürün kaydedildi.";
        }
    });

    [RelayCommand]
    private Task ReceiveAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        var qty = await dialogs.PromptAsync("Stok girişi", $"{Selected.Name} — gelen miktar ({Selected.Unit}):", "1");
        if (qty is null)
        {
            return;
        }

        if (!int.TryParse(qty, out var q))
        {
            ErrorMessage = "Miktar tam sayı olmalıdır.";
            return;
        }

        var note = await dialogs.PromptAsync("Stok girişi", "Açıklama (ör. fatura no) — isteğe bağlı:", string.Empty, required: false);
        var r = await inventory.ReceiveAsync(Selected.Id, q, Selected.CostMinor, note);
        if (Check(r))
        {
            InfoMessage = $"Yeni stok: {r.Value} {Selected.Unit}";
            await ReloadKeepingSelectionAsync();
        }
    });

    [RelayCommand]
    private Task AdjustAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            return;
        }

        var qty = await dialogs.PromptAsync("Sayım düzeltmesi", $"{Selected.Name} — mevcut: {Selected.StockText}\nEklenecek (+) veya düşülecek (−) miktar:", "-1");
        if (qty is null)
        {
            return;
        }

        if (!int.TryParse(qty.Replace("+", string.Empty, StringComparison.Ordinal).Replace('−', '-'), out var delta))
        {
            ErrorMessage = "Geçerli bir sayı girin.";
            return;
        }

        var reason = await dialogs.PromptAsync("Sayım düzeltmesi", "Gerekçe (denetim kaydına yazılır):");
        if (reason is null)
        {
            return;
        }

        var r = await inventory.AdjustAsync(Selected.Id, delta, reason);
        if (Check(r))
        {
            InfoMessage = $"Yeni stok: {r.Value} {Selected.Unit}";
            await ReloadKeepingSelectionAsync();
        }
    });

    [RelayCommand]
    private Task SellAsync() => RunAsync(async () =>
    {
        if (Selected is null)
        {
            ErrorMessage = "Soldaki listeden satılacak ürünü seçin.";
            return;
        }

        if (SaleCustomer is null)
        {
            var (resolved, error) = await ResultExtensions.ResolveTypedCustomerAsync(customers, CustomerSearch);
            if (resolved is null)
            {
                ErrorMessage = error;
                return;
            }

            SaleCustomer = resolved;
        }

        var price = Money.Parse(SalePriceText);
        if (!Check(price) || !int.TryParse(SaleQuantity, out var qty))
        {
            ErrorMessage ??= "Miktar tam sayı olmalıdır.";
            return;
        }

        var r = await inventory.SellAsync(new SellProductRequest(SaleCustomer.Id, Selected.Id, qty, price.Value.Minor, SaleStaff?.Id, SalePayment?.Value, _saleKey));
        if (Check(r))
        {
            _saleKey = Guid.NewGuid().ToString("N");
            InfoMessage = $"Satış kaydedildi: {Selected.Name} × {qty} — {SaleCustomer.FullName}";
            SaleCustomer = null;
            CustomerSearch = string.Empty;
            SaleQuantity = "1";
            await ReloadKeepingSelectionAsync();
        }
    });

    [RelayCommand]
    private void AddMaterial() => Materials.Add(new MaterialRow { Product = Products.FirstOrDefault() });

    [RelayCommand]
    private void RemoveMaterial(MaterialRow row) => Materials.Remove(row);

    [RelayCommand]
    private Task SaveMaterialsAsync() => RunAsync(async () =>
    {
        if (MaterialService is null)
        {
            ErrorMessage = "Önce bir hizmet seçin.";
            return;
        }

        var list = new List<(Guid, int)>();
        foreach (var m in Materials)
        {
            if (m.Product is null || !int.TryParse(m.Quantity, out var q) || q <= 0)
            {
                ErrorMessage = "Her satırda ürün ve pozitif miktar olmalıdır.";
                return;
            }

            list.Add((m.Product.Id, q));
        }

        if (Check(await inventory.SetServiceMaterialsAsync(MaterialService.Id, list)))
        {
            InfoMessage = $"{MaterialService.Name} tamamlandığında bu malzemeler stoktan otomatik düşülecek.";
        }
    });

    private async Task LoadAsync()
    {
        Products.Clear();
        foreach (var p in (await inventory.GetProductsAsync(includeInactive: true)).OrEmpty().Where(p => !LowOnly || p.IsLow))
        {
            Products.Add(p);
        }

        OnPropertyChanged(nameof(LowCount));
    }

    private async Task ReloadKeepingSelectionAsync()
    {
        var id = Selected?.Id;
        await LoadAsync();
        Selected = Products.FirstOrDefault(p => p.Id == id);
    }

    private async Task LoadMovementsAsync()
    {
        Movements.Clear();
        if (Selected is null)
        {
            return;
        }

        foreach (var m in (await inventory.GetMovementsAsync(Selected.Id)).OrEmpty())
        {
            Movements.Add(m);
        }
    }

    private async Task LoadMaterialsAsync()
    {
        Materials.Clear();
        if (MaterialService is null)
        {
            return;
        }

        foreach (var m in (await inventory.GetServiceMaterialsAsync(MaterialService.Id)).OrEmpty())
        {
            Materials.Add(new MaterialRow { Product = Products.FirstOrDefault(p => p.Id == m.ProductId), Quantity = m.Quantity.ToString(CultureInfo.InvariantCulture) });
        }
    }
}
