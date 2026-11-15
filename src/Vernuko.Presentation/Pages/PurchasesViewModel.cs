using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

/// <summary>A line on the goods receipt: unit cost without KDV, as on the supplier's invoice.</summary>
public sealed partial class ReceiptLine(ProductDto product, Action changed) : ObservableObject
{
    [ObservableProperty]
    private string _quantity = "1";

    [ObservableProperty]
    private string _unitCost = (product.CostMinor / 100m).ToString("0.00", TurkishText.Culture);

    [ObservableProperty]
    private int _vatRate = product.VatRate;

    [ObservableProperty]
    private string _discount = "0";

    public ProductDto Product { get; } = product;

    public int QuantityValue => int.TryParse(Quantity, NumberStyles.Integer, TurkishText.Culture, out var q) ? q : 0;

    public long UnitCostMinor => Money.Parse(UnitCost) is { IsSuccess: true } m ? m.Value.Minor : -1;

    public long DiscountMinor => string.IsNullOrWhiteSpace(Discount) ? 0 : Money.Parse(Discount) is { IsSuccess: true } m ? m.Value.Minor : -1;

    public long NetMinor => Math.Max(0, (QuantityValue * Math.Max(0, UnitCostMinor)) - Math.Max(0, DiscountMinor));

    public string NetText => Formatting.Money(NetMinor);

    partial void OnQuantityChanged(string value) => Refresh();

    partial void OnUnitCostChanged(string value) => Refresh();

    partial void OnVatRateChanged(int value) => Refresh();

    partial void OnDiscountChanged(string value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(NetText));
        changed();
    }
}

/// <summary>Alış ve tedarikçiler: suppliers and their accounts, goods receipts.</summary>
public sealed partial class PurchasesViewModel(ICommerceService commerce, IInventoryService inventory, TimeProvider time) : PageViewModel, IShellAware
{
    private string _receiptKey = Guid.NewGuid().ToString("N");
    private string _paymentKey = Guid.NewGuid().ToString("N");
    private IReadOnlyList<ProductDto> _allProducts = [];

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private SupplierStatementDto? _statement;

    // Supplier editor
    [ObservableProperty]
    private string _supplierName = string.Empty;

    [ObservableProperty]
    private string? _taxOffice;

    [ObservableProperty]
    private string? _taxNumber;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _iban;

    [ObservableProperty]
    private string? _supplierNote;

    // Payment to supplier
    [ObservableProperty]
    private string _payAmount = "0";

    [ObservableProperty]
    private Option<PaymentMethod>? _payMethod;

    // Goods receipt
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string? _documentNo;

    [ObservableProperty]
    private DateTime _documentDate = time.GetLocalNow().Date;

    [ObservableProperty]
    private bool _updateCosts = true;

    [ObservableProperty]
    private string _payNow = "0";

    public override string Title => "Alış ve Tedarikçiler";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<SupplierDto> Suppliers { get; } = [];

    public ObservableCollection<ReceiptLine> Lines { get; } = [];

    public ObservableCollection<ProductDto> ProductResults { get; } = [];

    public ObservableCollection<PurchaseSummaryDto> RecentReceipts { get; } = [];

    public IReadOnlyList<int> VatRates { get; } = [0, 1, 10, 20];

    public IReadOnlyList<Option<PaymentMethod>> PayMethods { get; } = Dialogs.PaymentDialogViewModel.MoneyMethods;

    public bool CanManage => Shell?.Has(Permission.PurchasesManage) ?? false;

    public long NetMinor => Lines.Sum(l => l.NetMinor);

    public long VatMinor => Lines.Sum(l => Commerce.Vat.On(l.NetMinor, l.VatRate));

    public string TotalsText => $"KDV hariç {Formatting.Money(NetMinor)} + KDV {Formatting.Money(VatMinor)} = {Formatting.Money(NetMinor + VatMinor)}";

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(LoadAsync);

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        SupplierName = value?.Name ?? string.Empty;
        TaxOffice = value?.TaxOffice;
        TaxNumber = value?.TaxNumber;
        Phone = value?.Phone;
        Iban = value?.Iban;
        SupplierNote = value?.Note;
        _ = LoadStatementAsync();
    }

    partial void OnCodeChanged(string value)
    {
        ProductResults.Clear();
        var t = value.Trim();
        if (t.Length < 2)
        {
            return;
        }

        foreach (var p in _allProducts.Where(p => p.Name.Contains(t, StringComparison.CurrentCultureIgnoreCase) || (p.Sku?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) || (p.Barcode?.StartsWith(t, StringComparison.Ordinal) ?? false)).Take(8))
        {
            ProductResults.Add(p);
        }
    }

    [RelayCommand]
    private void NewSupplier() => SelectedSupplier = null;

    [RelayCommand]
    private Task SaveSupplierAsync() => RunAsync(async () =>
    {
        var s = SelectedSupplier;
        var r = await commerce.SaveSupplierAsync(new SupplierDto(s?.Id ?? Guid.Empty, SupplierName, TaxOffice, TaxNumber, Phone, s?.Email, s?.Address, Iban, SupplierNote, true, 0));
        if (Check(r))
        {
            await LoadAsync();
            SelectedSupplier = Suppliers.FirstOrDefault(x => x.Id == r.Value);
            InfoMessage = "Tedarikçi kaydedildi.";
        }
    });

    [RelayCommand]
    private Task PaySupplierAsync() => RunAsync(async () =>
    {
        if (SelectedSupplier is null || PayMethod is null || Money.Parse(PayAmount) is not { IsSuccess: true } amount)
        {
            ErrorMessage = "Tedarikçi, tutar ve ödeme yöntemi seçin.";
            return;
        }

        var r = await commerce.PaySupplierAsync(new SupplierPaymentRequest(SelectedSupplier.Id, amount.Value.Minor, PayMethod.Value, null, _paymentKey));
        if (Check(r))
        {
            _paymentKey = Guid.NewGuid().ToString("N");
            PayAmount = "0";
            InfoMessage = "Ödeme kaydedildi.";
            var id = SelectedSupplier.Id;
            await LoadAsync();
            SelectedSupplier = Suppliers.FirstOrDefault(x => x.Id == id);
        }
    });

    [RelayCommand]
    private Task AddCodeAsync() => RunAsync(async () =>
    {
        var found = (await commerce.FindProductAsync(Code.Trim())).Value ?? (ProductResults.Count == 1 ? ProductResults[0] : null);
        if (found is null)
        {
            ErrorMessage = $"\"{Code.Trim()}\" ürünü yok. Önce Ürünler ve Stok ekranından ekleyin.";
            return;
        }

        AddProduct(found);
    });

    [RelayCommand]
    private void AddProduct(ProductDto product)
    {
        var existing = Lines.FirstOrDefault(l => l.Product.Id == product.Id);
        if (existing is not null)
        {
            existing.Quantity = (existing.QuantityValue + 1).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            Lines.Add(new ReceiptLine(product, Recalculate));
        }

        Code = string.Empty;
        ProductResults.Clear();
        Recalculate();
    }

    [RelayCommand]
    private void RemoveLine(ReceiptLine line)
    {
        Lines.Remove(line);
        Recalculate();
    }

    [RelayCommand]
    private Task SaveReceiptAsync() => RunAsync(async () =>
    {
        if (SelectedSupplier is null)
        {
            ErrorMessage = "Soldaki listeden tedarikçiyi seçin.";
            return;
        }

        if (Lines.Any(l => l.QuantityValue < 1 || l.UnitCostMinor < 0 || l.DiscountMinor < 0))
        {
            ErrorMessage = "Miktar, maliyet ve indirim alanlarını kontrol edin.";
            return;
        }

        var pay = Money.Parse(PayNow) is { IsSuccess: true } p ? p.Value.Minor : 0;
        var r = await commerce.CreatePurchaseAsync(new CreatePurchaseRequest(SelectedSupplier.Id, DocumentNo, DateOnly.FromDateTime(DocumentDate),
            Lines.Select(l => new PurchaseLineRequest(l.Product.Id, l.QuantityValue, l.UnitCostMinor, l.VatRate, l.DiscountMinor)).ToList(),
            UpdateCosts, pay, pay > 0 ? PayMethod?.Value ?? PaymentMethod.Cash : null, null, _receiptKey));
        if (!Check(r))
        {
            return;
        }

        InfoMessage = $"{r.Value.Summary.Number} mal kabul kaydedildi: {Formatting.Money(r.Value.Summary.TotalMinor)}. Stok güncellendi.";
        Lines.Clear();
        DocumentNo = null;
        PayNow = "0";
        _receiptKey = Guid.NewGuid().ToString("N");
        var id = SelectedSupplier.Id;
        await LoadAsync();
        SelectedSupplier = Suppliers.FirstOrDefault(x => x.Id == id);
        Recalculate();
    });

    private async Task LoadAsync()
    {
        PayMethod ??= PayMethods.FirstOrDefault();
        _allProducts = (await inventory.GetProductsAsync()).OrEmpty();
        Suppliers.Clear();
        foreach (var s in (await commerce.GetSuppliersAsync()).OrEmpty())
        {
            Suppliers.Add(s);
        }

        RecentReceipts.Clear();
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        foreach (var p in (await commerce.GetPurchasesAsync(today.AddDays(-60), today)).OrEmpty().Take(50))
        {
            RecentReceipts.Add(p);
        }
    }

    private async Task LoadStatementAsync() =>
        Statement = SelectedSupplier is null ? null : (await commerce.GetSupplierStatementAsync(SelectedSupplier.Id)) is { IsSuccess: true } s ? s.Value : null;

    private void Recalculate()
    {
        OnPropertyChanged(nameof(NetMinor));
        OnPropertyChanged(nameof(VatMinor));
        OnPropertyChanged(nameof(TotalsText));
    }
}
