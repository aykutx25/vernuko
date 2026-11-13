using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

/// <summary>Ayarlar › Prim kuralları. The most specific rule wins (personel + hizmet &gt; personel &gt; hizmet &gt; genel).</summary>
public sealed partial class CommissionRulesViewModel(ICommissionService commissions, ICatalogService catalog, IInventoryService inventory) : ViewModelBase
{
    private IReadOnlyList<ServiceDto> _services = [];
    private IReadOnlyList<ProductDto> _products = [];

    [ObservableProperty]
    private CommissionRuleDto? _selected;

    [ObservableProperty]
    private Option<CommissionBasis>? _basis;

    [ObservableProperty]
    private Option<Guid?>? _staff;

    [ObservableProperty]
    private Option<Guid?>? _item;

    [ObservableProperty]
    private Option<CommissionRateKind>? _rateKind;

    [ObservableProperty]
    private string _value = "10";

    [ObservableProperty]
    private bool _isActive = true;

    public ObservableCollection<CommissionRuleDto> Rules { get; } = [];

    public IReadOnlyList<Option<CommissionBasis>> BasisOptions { get; } =
        Enum.GetValues<CommissionBasis>().Select(b => new Option<CommissionBasis>(CommissionRule.BasisName(b), b)).ToList();

    public IReadOnlyList<Option<CommissionRateKind>> RateKindOptions { get; } =
    [
        new("Yüzde (%)", CommissionRateKind.Percent), new("Sabit tutar (₺)", CommissionRateKind.FixedAmount),
    ];

    public ObservableCollection<Option<Guid?>> StaffOptions { get; } = [];

    public ObservableCollection<Option<Guid?>> ItemOptions { get; } = [];

    public async Task LoadAsync()
    {
        _services = (await catalog.GetServicesAsync(includeInactive: true)).OrEmpty();
        _products = (await inventory.GetProductsAsync(includeInactive: true)).OrEmpty();
        StaffOptions.Clear();
        StaffOptions.Add(new Option<Guid?>("Tüm personel", null));
        foreach (var s in (await catalog.GetStaffAsync(includeInactive: true)).OrEmpty())
        {
            StaffOptions.Add(new Option<Guid?>(s.DisplayName, s.Id));
        }

        Basis ??= BasisOptions[0];
        RateKind ??= RateKindOptions[0];
        Staff ??= StaffOptions[0];
        FillItems();
        await ReloadAsync();
    }

    partial void OnBasisChanged(Option<CommissionBasis>? value) => FillItems();

    partial void OnSelectedChanged(CommissionRuleDto? value)
    {
        if (value is null)
        {
            return;
        }

        Basis = BasisOptions.First(b => b.Value == value.Basis);
        Staff = StaffOptions.FirstOrDefault(s => s.Value == value.StaffMemberId) ?? StaffOptions[0];
        Item = ItemOptions.FirstOrDefault(i => i.Value == value.ItemId) ?? ItemOptions[0];
        RateKind = RateKindOptions.First(k => k.Value == value.RateKind);
        Value = value.RateKind == CommissionRateKind.Percent
            ? (value.PercentBasisPoints / 100m).ToString("0.##", TurkishText.Culture)
            : (value.FixedMinor / 100m).ToString("0.##", TurkishText.Culture);
        IsActive = value.IsActive;
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        Basis = BasisOptions[0];
        Staff = StaffOptions.FirstOrDefault();
        RateKind = RateKindOptions[0];
        Value = "10";
        IsActive = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        // Same rules as amounts ("12,5", "12.5", "1.250"): with tr-TR parsing "12.5" became 125 (a %125 rule).
        var parsed = Money.Parse(Value);
        if (parsed.IsFailure)
        {
            ErrorMessage = "Değer geçersiz. Örnek: 12,5 veya 150,00";
            return;
        }

        var v = parsed.Value.Minor / 100m;

        var kind = RateKind?.Value ?? CommissionRateKind.Percent;
        var dto = new CommissionRuleDto(
            Selected?.Id ?? Guid.Empty, Basis?.Value ?? CommissionBasis.ServiceDelivered, Staff?.Value, string.Empty, Item?.Value, string.Empty, kind,
            kind == CommissionRateKind.Percent ? (int)Math.Round(v * 100) : 0,
            kind == CommissionRateKind.FixedAmount ? (long)Math.Round(v * 100) : 0,
            IsActive);
        if (Check(await commissions.SaveRuleAsync(dto)))
        {
            InfoMessage = "Prim kuralı kaydedildi. Yalnızca bundan sonraki işlemlere uygulanır.";
            await ReloadAsync();
        }
    });

    private void FillItems()
    {
        var basis = Basis?.Value ?? CommissionBasis.ServiceDelivered;
        ItemOptions.Clear();
        if (basis == CommissionBasis.ProductSold)
        {
            ItemOptions.Add(new Option<Guid?>("Tüm ürünler", null));
            foreach (var p in _products)
            {
                ItemOptions.Add(new Option<Guid?>(p.Name, p.Id));
            }
        }
        else
        {
            ItemOptions.Add(new Option<Guid?>("Tüm hizmetler", null));
            foreach (var s in _services)
            {
                ItemOptions.Add(new Option<Guid?>(s.Name, s.Id));
            }
        }

        Item = ItemOptions[0];
    }

    private async Task ReloadAsync()
    {
        Rules.Clear();
        foreach (var r in (await commissions.GetRulesAsync()).OrEmpty())
        {
            Rules.Add(r);
        }
    }
}

/// <summary>Ayarlar › Onam formları. Text is versioned: every recorded consent refers to the exact wording.</summary>
public sealed partial class ConsentTemplatesViewModel(ICustomerRecordsService records) : ViewModelBase
{
    [ObservableProperty]
    private ConsentTemplateDto? _selected;

    [ObservableProperty]
    private string _templateTitle = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    public ObservableCollection<ConsentTemplateDto> Templates { get; } = [];

    public string Disclaimer => "Vernuko hukuki metin sağlamaz. Bilgilendirme ve onam metinlerini hukuk danışmanınızla hazırlayın. Metin değiştiğinde sürüm numarası artar; eski onaylar eski sürüme bağlı kalır.";

    public async Task LoadAsync()
    {
        Templates.Clear();
        foreach (var t in (await records.GetTemplatesAsync(includeInactive: true)).OrEmpty())
        {
            Templates.Add(t);
        }
    }

    partial void OnSelectedChanged(ConsentTemplateDto? value)
    {
        if (value is null)
        {
            return;
        }

        TemplateTitle = value.Title;
        Body = value.Body;
        IsActive = value.IsActive;
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        TemplateTitle = string.Empty;
        Body = string.Empty;
        IsActive = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var r = await records.SaveTemplateAsync(new ConsentTemplateDto(Selected?.Id ?? Guid.Empty, TemplateTitle, Body, 0, IsActive));
        if (Check(r))
        {
            await LoadAsync();
            Selected = Templates.FirstOrDefault(t => t.Id == r.Value);
            InfoMessage = "Onam formu kaydedildi.";
        }
    });
}
