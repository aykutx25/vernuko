using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Commerce;
using Vernuko.Core;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

/// <summary>One product on the counter: quantity, price (KDV included) and discount can be changed.</summary>
public sealed partial class CartLine(ProductDto product, Action changed) : ObservableObject
{
    [ObservableProperty]
    private string _quantity = "1";

    [ObservableProperty]
    private string _unitPrice = (product.SalePriceMinor / 100m).ToString("0.00", TurkishText.Culture);

    [ObservableProperty]
    private string _discount = "0";

    public ProductDto Product { get; } = product;

    public int QuantityValue => int.TryParse(Quantity, NumberStyles.Integer, TurkishText.Culture, out var q) ? q : 0;

    public long UnitPriceMinor => Money.Parse(UnitPrice) is { IsSuccess: true } m ? m.Value.Minor : -1;

    public long DiscountMinor => string.IsNullOrWhiteSpace(Discount) ? 0 : Money.Parse(Discount) is { IsSuccess: true } m ? m.Value.Minor : -1;

    public long TotalMinor => Math.Max(0, (QuantityValue * Math.Max(0, UnitPriceMinor)) - Math.Max(0, DiscountMinor));

    public string TotalText => Formatting.Money(TotalMinor);

    /// <summary>Selling more than is on the shelf is allowed (counts are often behind); the line says so.</summary>
    public bool ExceedsStock => QuantityValue > Product.Stock;

    partial void OnQuantityChanged(string value) => Refresh();

    partial void OnUnitPriceChanged(string value) => Refresh();

    partial void OnDiscountChanged(string value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(ExceedsStock));
        changed();
    }
}

public sealed partial class ReturnRow(SaleLineDto line) : ObservableObject
{
    [ObservableProperty]
    private string _quantity = "0";

    public SaleLineDto Line { get; } = line;

    public int QuantityValue => int.TryParse(Quantity, NumberStyles.Integer, TurkishText.Culture, out var q) ? q : 0;
}

/// <summary>
/// Satış ekranı (Stok mode, and Teknik Servis shops that sell): scan or type a code, adjust quantities, take payment.
/// Not a fiscal receipt: the screen says so (VUK 483: retail sales need the ÖKC's receipt).
/// </summary>
public sealed partial class SalesViewModel(ICommerceService commerce, IInventoryService inventory, ICustomerService customers, TimeProvider time) : PageViewModel, IShellAware
{
    private readonly Debouncer _debounce = new(TimeSpan.FromMilliseconds(250));
    private string _saleKey = Guid.NewGuid().ToString("N");
    private string _returnKey = Guid.NewGuid().ToString("N");
    private IReadOnlyList<ProductDto> _allProducts = [];

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerListItem? _customer;

    [ObservableProperty]
    private string _documentDiscount = "0";

    [ObservableProperty]
    private string _cash = "0";

    [ObservableProperty]
    private string _card = "0";

    [ObservableProperty]
    private string _transfer = "0";

    [ObservableProperty]
    private string? _note;

    [ObservableProperty]
    private SaleSummaryDto? _selectedSale;

    [ObservableProperty]
    private SaleDto? _saleDetail;

    [ObservableProperty]
    private DayEndDto? _today;

    [ObservableProperty]
    private string _refundCash = "0";

    public override string Title => "Satış";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<CartLine> Cart { get; } = [];

    public ObservableCollection<ProductDto> ProductResults { get; } = [];

    public ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public ObservableCollection<SaleSummaryDto> TodaySales { get; } = [];

    public ObservableCollection<ReturnRow> ReturnRows { get; } = [];

    public bool CanReturn => Shell?.Has(Permission.PaymentsReverse) ?? false;

    public bool IsWalkIn => Customer is null;

    public string CustomerText => Customer is null ? "Kayıtsız müşteri (peşin)" : $"{Customer.FullName} — kalan tutar hesabına yazılır";

    public long GrossMinor => Cart.Sum(l => l.TotalMinor);

    public long DocumentDiscountMinor => string.IsNullOrWhiteSpace(DocumentDiscount) ? 0 : Money.Parse(DocumentDiscount) is { IsSuccess: true } m ? m.Value.Minor : 0;

    public long TotalMinor => Math.Max(0, GrossMinor - DocumentDiscountMinor);

    public long PaidMinor => Parse(Cash) + Parse(Card) + Parse(Transfer);

    /// <summary>Cash handed over above the total: the change to give back.</summary>
    public long ChangeMinor => Math.Max(0, PaidMinor - TotalMinor);

    public long OnAccountMinor => Math.Max(0, TotalMinor - PaidMinor);

    public string TotalText => Formatting.Money(TotalMinor);

    public string PaymentText => ChangeMinor > 0 ? $"Para üstü: {Formatting.Money(ChangeMinor)}"
        : OnAccountMinor > 0 ? (IsWalkIn ? $"Eksik ödeme: {Formatting.Money(OnAccountMinor)}" : $"Hesaba yazılacak (veresiye): {Formatting.Money(OnAccountMinor)}")
        : "Ödeme tam.";

    public bool HasStockWarning => Cart.Any(l => l.ExceedsStock);

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        _allProducts = (await inventory.GetProductsAsync()).OrEmpty();
        await LoadTodayAsync();
    });

    partial void OnCodeChanged(string value)
    {
        ProductResults.Clear();
        var t = value?.Trim() ?? string.Empty;
        if (t.Length < 2)
        {
            return;
        }

        foreach (var p in _allProducts.Where(p => p.Name.Contains(t, StringComparison.CurrentCultureIgnoreCase) || (p.Sku?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) || (p.Barcode?.StartsWith(t, StringComparison.Ordinal) ?? false)).Take(8))
        {
            ProductResults.Add(p);
        }
    }

    partial void OnCustomerSearchChanged(string value) => _ = _debounce.RunAsync(async _ =>
    {
        CustomerResults.Clear();
        if (value.Trim().Length < 2 || (Customer is not null && value == Customer.FullName))
        {
            return;
        }

        foreach (var c in (await customers.SearchAsync(value, take: 6)).OrEmpty())
        {
            CustomerResults.Add(c);
        }
    });

    partial void OnCustomerChanged(CustomerListItem? value)
    {
        CustomerResults.Clear();
        OnPropertyChanged(nameof(IsWalkIn));
        OnPropertyChanged(nameof(CustomerText));
        Recalculate();
    }

    partial void OnDocumentDiscountChanged(string value) => Recalculate();

    partial void OnCashChanged(string value) => Recalculate();

    partial void OnCardChanged(string value) => Recalculate();

    partial void OnTransferChanged(string value) => Recalculate();

    partial void OnSelectedSaleChanged(SaleSummaryDto? value) => _ = LoadSaleAsync(value);

    /// <summary>Scanner or Enter: an exact barcode/code adds the product; otherwise the first match from the list.</summary>
    [RelayCommand]
    private Task AddCodeAsync() => RunAsync(async () =>
    {
        var typed = Code.Trim();
        if (typed.Length == 0)
        {
            return;
        }

        var found = (await commerce.FindProductAsync(typed)).Value ?? (ProductResults.Count == 1 ? ProductResults[0] : null);
        if (found is null)
        {
            ErrorMessage = $"\"{typed}\" barkodlu ya da kodlu ürün yok. Ürünler ve Stok ekranından ekleyin.";
            return;
        }

        AddProduct(found);
    });

    [RelayCommand]
    private void AddProduct(ProductDto product)
    {
        var existing = Cart.FirstOrDefault(l => l.Product.Id == product.Id);
        if (existing is not null)
        {
            existing.Quantity = (existing.QuantityValue + 1).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            Cart.Add(new CartLine(product, Recalculate));
        }

        Code = string.Empty;
        ProductResults.Clear();
        Recalculate();
    }

    [RelayCommand]
    private void RemoveLine(CartLine line)
    {
        Cart.Remove(line);
        Recalculate();
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        Customer = null;
        CustomerSearch = string.Empty;
    }

    [RelayCommand]
    private void PickCustomer(CustomerListItem c)
    {
        Customer = c;
        CustomerSearch = c.FullName;
    }

    [RelayCommand]
    private void AllCash() => SetPayment(cash: TotalMinor);

    [RelayCommand]
    private void AllCard() => SetPayment(card: TotalMinor);

    [RelayCommand]
    private Task CompleteAsync() => RunAsync(async () =>
    {
        if (Cart.Count == 0)
        {
            ErrorMessage = "Önce ürün okutun.";
            return;
        }

        if (Cart.Any(l => l.QuantityValue < 1 || l.UnitPriceMinor < 0 || l.DiscountMinor < 0))
        {
            ErrorMessage = "Miktar, fiyat ve indirim alanlarını kontrol edin (ör. 2 ve 45,00).";
            return;
        }

        // Change is handed back in cash: only what stays is recorded.
        var card = Parse(Card);
        var transfer = Parse(Transfer);
        var cash = Math.Max(0, Math.Min(Parse(Cash), TotalMinor - card - transfer));
        var r = await commerce.CreateSaleAsync(new CreateSaleRequest(Customer?.Id, Cart.Select(l => new SaleLineRequest(l.Product.Id, l.QuantityValue, l.UnitPriceMinor, l.DiscountMinor)).ToList(),
            DocumentDiscountMinor, cash, card, transfer, Note, _saleKey));
        if (!Check(r))
        {
            return;
        }

        var change = ChangeMinor;
        InfoMessage = $"{r.Value.Number} kaydedildi: {Formatting.Money(r.Value.TotalMinor)}" + (change > 0 ? $". Para üstü {Formatting.Money(change)}." : ".")
            + (r.Value.OnAccountMinor > 0 ? $" {Formatting.Money(r.Value.OnAccountMinor)} müşterinin hesabına yazıldı." : string.Empty);
        NewSale();
        _allProducts = (await inventory.GetProductsAsync()).OrEmpty();
        await LoadTodayAsync();
    });

    [RelayCommand]
    private void NewSale()
    {
        Cart.Clear();
        Customer = null;
        CustomerSearch = string.Empty;
        DocumentDiscount = "0";
        Cash = "0";
        Card = "0";
        Transfer = "0";
        Note = null;
        _saleKey = Guid.NewGuid().ToString("N");
        Recalculate();
    }

    [RelayCommand]
    private Task ReturnAsync() => RunAsync(async () =>
    {
        if (SaleDetail is null)
        {
            return;
        }

        var lines = ReturnRows.Where(r => r.QuantityValue > 0).Select(r => new ReturnLineRequest(r.Line.Id, r.QuantityValue)).ToList();
        if (lines.Count == 0)
        {
            ErrorMessage = "İade edilecek miktarı girin.";
            return;
        }

        var r = await commerce.CreateReturnAsync(new CreateReturnRequest(SaleDetail.Id, lines, Parse(RefundCash), 0, 0, null, _returnKey));
        if (!Check(r))
        {
            return;
        }

        InfoMessage = $"{r.Value.Number} iade kaydedildi: {Formatting.Money(r.Value.TotalMinor)}" + (r.Value.OnAccountMinor > 0 ? $"; {Formatting.Money(r.Value.OnAccountMinor)} müşterinin hesabına alacak yazıldı." : ".");
        _returnKey = Guid.NewGuid().ToString("N");
        RefundCash = "0";
        _allProducts = (await inventory.GetProductsAsync()).OrEmpty();
        await LoadTodayAsync();
    });

    private async Task LoadTodayAsync()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        Today = (await commerce.GetDayEndAsync(today)) is { IsSuccess: true } d ? d.Value : null;
        TodaySales.Clear();
        foreach (var s in (await commerce.GetSalesAsync(today, today)).OrEmpty())
        {
            TodaySales.Add(s);
        }
    }

    private async Task LoadSaleAsync(SaleSummaryDto? summary)
    {
        ReturnRows.Clear();
        SaleDetail = null;
        if (summary is null)
        {
            return;
        }

        var r = await commerce.GetSaleAsync(summary.Id);
        if (r.IsFailure)
        {
            return;
        }

        SaleDetail = r.Value;
        if (r.Value.Kind == SaleKind.Sale)
        {
            foreach (var l in r.Value.Lines.Where(l => l.ReturnableQuantity > 0))
            {
                ReturnRows.Add(new ReturnRow(l));
            }
        }
    }

    private void SetPayment(long cash = 0, long card = 0)
    {
        Cash = (cash / 100m).ToString("0.00", TurkishText.Culture);
        Card = (card / 100m).ToString("0.00", TurkishText.Culture);
        Transfer = "0";
    }

    private void Recalculate()
    {
        foreach (var name in new[] { nameof(GrossMinor), nameof(TotalMinor), nameof(TotalText), nameof(PaidMinor), nameof(ChangeMinor), nameof(OnAccountMinor), nameof(PaymentText), nameof(HasStockWarning) })
        {
            OnPropertyChanged(name);
        }
    }

    private static long Parse(string? text) => string.IsNullOrWhiteSpace(text) ? 0 : Money.Parse(text) is { IsSuccess: true } m ? Math.Max(0, m.Value.Minor) : 0;
}
