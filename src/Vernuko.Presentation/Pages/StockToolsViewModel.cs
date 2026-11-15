using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed partial class CountRow(ProductDto product) : ObservableObject
{
    [ObservableProperty]
    private string _counted = string.Empty;

    public ProductDto Product { get; } = product;

    public int? CountedValue => int.TryParse(Counted, NumberStyles.Integer, TurkishText.Culture, out var q) && q >= 0 ? q : null;

    public string DifferenceText => CountedValue is { } c && c != Product.Stock ? (c - Product.Stock).ToString("+0;−0", TurkishText.Culture) : string.Empty;

    partial void OnCountedChanged(string value) => OnPropertyChanged(nameof(DifferenceText));
}

/// <summary>Sayım (count the shelves, post the differences once), sipariş önerisi (by supplier) and stock value.</summary>
public sealed partial class StockToolsViewModel(ICommerceService commerce, IInventoryService inventory) : PageViewModel, IShellAware
{
    private string _countKey = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private string? _countNote;

    [ObservableProperty]
    private StockValueDto? _value;

    public override string Title => "Sayım ve Sipariş";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<CountRow> Rows { get; } = [];

    public ObservableCollection<CountRow> VisibleRows { get; } = [];

    public ObservableCollection<ReorderGroupDto> Reorder { get; } = [];

    public bool CanManage => Shell?.Has(Permission.InventoryManage) ?? false;

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(LoadAsync);

    partial void OnFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task ApplyCountAsync() => RunAsync(async () =>
    {
        var lines = Rows.Where(r => r.CountedValue is not null).Select(r => new StockCountLine(r.Product.Id, r.CountedValue!.Value)).ToList();
        if (lines.Count == 0)
        {
            ErrorMessage = "Saydığınız ürünlerin miktarını yazın; boş bıraktıklarınız değişmez.";
            return;
        }

        var r = await commerce.ApplyStockCountAsync(new StockCountRequest(lines, CountNote, _countKey));
        if (!Check(r))
        {
            return;
        }

        InfoMessage = $"{r.Value.CountedProducts} ürün sayıldı; {r.Value.AdjustedProducts} üründe fark kaydedildi ({r.Value.QuantityDifference:+0;−0;0} adet, {Formatting.Money(r.Value.ValueDifferenceMinor)}).";
        _countKey = Guid.NewGuid().ToString("N");
        CountNote = null;
        await LoadAsync();
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadAsync);

    private async Task LoadAsync()
    {
        Rows.Clear();
        foreach (var p in (await inventory.GetProductsAsync()).OrEmpty())
        {
            Rows.Add(new CountRow(p));
        }

        ApplyFilter();
        Reorder.Clear();
        foreach (var g in (await commerce.GetReorderSuggestionsAsync()).OrEmpty())
        {
            Reorder.Add(g);
        }

        Value = (await commerce.GetStockValueAsync()) is { IsSuccess: true } v ? v.Value : null;
    }

    private void ApplyFilter()
    {
        VisibleRows.Clear();
        var t = Filter.Trim();
        foreach (var r in Rows.Where(r => t.Length == 0 || r.Product.Name.Contains(t, StringComparison.CurrentCultureIgnoreCase)
            || (r.Product.Barcode?.Contains(t, StringComparison.Ordinal) ?? false) || (r.Product.ShelfLocation?.Contains(t, StringComparison.CurrentCultureIgnoreCase) ?? false)))
        {
            VisibleRows.Add(r);
        }
    }
}
