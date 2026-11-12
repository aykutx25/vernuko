using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

/// <summary>Services, rooms/devices and package definitions.</summary>
public sealed partial class CatalogViewModel(CatalogService catalog, SettingsService settings) : PageViewModel, IShellAware
{
    [ObservableProperty]
    private ServiceDto? _selectedService;

    [ObservableProperty]
    private ResourceDto? _selectedResource;

    [ObservableProperty]
    private PackageTemplateDto? _selectedTemplate;

    // Service form
    [ObservableProperty]
    private string _serviceName = string.Empty;

    [ObservableProperty]
    private string? _serviceCategory;

    [ObservableProperty]
    private string _serviceDuration = "30";

    [ObservableProperty]
    private string _servicePrep = "0";

    [ObservableProperty]
    private string _serviceCleanup = "0";

    [ObservableProperty]
    private string _servicePrice = "0";

    [ObservableProperty]
    private Option<ResourceKind?>? _serviceRequires;

    [ObservableProperty]
    private bool _serviceActive = true;

    // Resource form
    [ObservableProperty]
    private string _resourceName = string.Empty;

    [ObservableProperty]
    private Option<ResourceKind>? _resourceKind;

    [ObservableProperty]
    private bool _resourceActive = true;

    // Package template form
    [ObservableProperty]
    private string _templateName = string.Empty;

    [ObservableProperty]
    private ServiceDto? _templateService;

    [ObservableProperty]
    private string _templateSessions = "8";

    [ObservableProperty]
    private string _templatePrice = "0";

    [ObservableProperty]
    private string _templateValidity = "365";

    [ObservableProperty]
    private bool _templateActive = true;

    [ObservableProperty]
    private bool _showPackages;

    public override string Title => "Hizmetler";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<ServiceDto> Services { get; } = [];

    public ObservableCollection<ResourceDto> Resources { get; } = [];

    public ObservableCollection<PackageTemplateDto> Templates { get; } = [];

    /// <summary>Rooms/devices the edited service is limited to (none ticked = no limit).</summary>
    public ObservableCollection<ResourceChoice> ServiceResourceChoices { get; } = [];

    public IReadOnlyList<Option<ResourceKind?>> RequirementOptions { get; } =
        new[] { new Option<ResourceKind?>("Gerekmez", null) }.Concat(Enum.GetValues<ResourceKind>().Select(k => new Option<ResourceKind?>(BusinessProfileCatalog.ResourceKindName(k), k))).ToList();

    public IReadOnlyList<Option<ResourceKind>> KindOptions { get; } =
        Enum.GetValues<ResourceKind>().Select(k => new Option<ResourceKind>(BusinessProfileCatalog.ResourceKindName(k), k)).ToList();

    public bool CanManage => Shell?.Has(Permission.CatalogManage) ?? false;

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        var info = await settings.GetBusinessInfoAsync();
        ShowPackages = info?.Features.Contains(ProfileFeature.SessionPackages) == true;
        await ReloadAsync();
        NewService();
        NewResource();
        NewTemplate();
    });

    partial void OnSelectedServiceChanged(ServiceDto? value)
    {
        if (value is null)
        {
            return;
        }

        ServiceName = value.Name;
        ServiceCategory = value.Category;
        ServiceDuration = value.DurationMinutes.ToString(CultureInfo.InvariantCulture);
        ServicePrep = value.PrepMinutes.ToString(CultureInfo.InvariantCulture);
        ServiceCleanup = value.CleanupMinutes.ToString(CultureInfo.InvariantCulture);
        ServicePrice = (value.PriceMinor / 100m).ToString("0.##", TurkishText.Culture);
        ServiceRequires = RequirementOptions.FirstOrDefault(o => o.Value == value.RequiredResourceKind);
        ServiceActive = value.IsActive;
        foreach (var c in ServiceResourceChoices)
        {
            c.IsSelected = value.AllowedResourceIds?.Contains(c.Resource.Id) == true;
        }
    }

    partial void OnSelectedResourceChanged(ResourceDto? value)
    {
        if (value is null)
        {
            return;
        }

        ResourceName = value.Name;
        ResourceKind = KindOptions.FirstOrDefault(k => k.Value == value.Kind);
        ResourceActive = value.IsActive;
    }

    partial void OnSelectedTemplateChanged(PackageTemplateDto? value)
    {
        if (value is null)
        {
            return;
        }

        TemplateName = value.Name;
        TemplateService = Services.FirstOrDefault(s => s.Id == value.ServiceId);
        TemplateSessions = value.SessionCount.ToString(CultureInfo.InvariantCulture);
        TemplatePrice = (value.PriceMinor / 100m).ToString("0.##", TurkishText.Culture);
        TemplateValidity = value.ValidityDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        TemplateActive = value.IsActive;
    }

    [RelayCommand]
    private void NewService()
    {
        SelectedService = null;
        ServiceName = string.Empty;
        ServiceCategory = null;
        ServiceDuration = "30";
        ServicePrep = "0";
        ServiceCleanup = "0";
        ServicePrice = "0";
        ServiceRequires = RequirementOptions[0];
        ServiceActive = true;
        foreach (var c in ServiceResourceChoices)
        {
            c.IsSelected = false;
        }
    }

    [RelayCommand]
    private Task SaveServiceAsync() => RunAsync(async () =>
    {
        var price = Money.Parse(ServicePrice);
        if (!Check(price))
        {
            return;
        }

        if (!int.TryParse(ServiceDuration, out var duration) || !int.TryParse(ServicePrep, out var prep) || !int.TryParse(ServiceCleanup, out var cleanup))
        {
            ErrorMessage = "Süreler dakika olarak tam sayı girilmelidir.";
            return;
        }

        var allowed = ServiceResourceChoices.Where(c => c.IsSelected).Select(c => c.Resource.Id).ToList();
        var r = await catalog.SaveServiceAsync(new ServiceDto(SelectedService?.Id ?? Guid.Empty, ServiceName, ServiceCategory, duration, prep, cleanup, price.Value.Minor, ServiceRequires?.Value, ServiceActive, allowed));
        if (Check(r))
        {
            await ReloadAsync();
            SelectedService = Services.FirstOrDefault(s => s.Id == r.Value);
            InfoMessage = "Hizmet kaydedildi.";
        }
    });

    [RelayCommand]
    private void NewResource()
    {
        SelectedResource = null;
        ResourceName = string.Empty;
        ResourceKind = KindOptions[0];
        ResourceActive = true;
    }

    [RelayCommand]
    private Task SaveResourceAsync() => RunAsync(async () =>
    {
        var r = await catalog.SaveResourceAsync(new ResourceDto(SelectedResource?.Id ?? Guid.Empty, ResourceName, ResourceKind?.Value ?? Core.ResourceKind.Room, ResourceActive, null));
        if (Check(r))
        {
            await ReloadAsync();
            SelectedResource = Resources.FirstOrDefault(x => x.Id == r.Value);
            InfoMessage = "Kaydedildi.";
        }
    });

    [RelayCommand]
    private void NewTemplate()
    {
        SelectedTemplate = null;
        TemplateName = string.Empty;
        TemplateService = Services.FirstOrDefault();
        TemplateSessions = "8";
        TemplatePrice = "0";
        TemplateValidity = "365";
        TemplateActive = true;
    }

    [RelayCommand]
    private Task SaveTemplateAsync() => RunAsync(async () =>
    {
        var price = Money.Parse(TemplatePrice);
        if (!Check(price))
        {
            return;
        }

        if (TemplateService is null || !int.TryParse(TemplateSessions, out var sessions))
        {
            ErrorMessage = "Hizmet ve seans sayısı girilmelidir.";
            return;
        }

        int? validity = int.TryParse(TemplateValidity, out var v) ? v : null;
        var r = await catalog.SavePackageTemplateAsync(new PackageTemplateDto(SelectedTemplate?.Id ?? Guid.Empty, TemplateName, TemplateService.Id, TemplateService.Name, sessions, price.Value.Minor, validity, TemplateActive));
        if (Check(r))
        {
            await ReloadAsync();
            SelectedTemplate = Templates.FirstOrDefault(t => t.Id == r.Value);
            InfoMessage = "Paket tanımı kaydedildi.";
        }
    });

    private async Task ReloadAsync()
    {
        Services.Clear();
        foreach (var s in (await catalog.GetServicesAsync(includeInactive: true)).OrEmpty())
        {
            Services.Add(s);
        }

        Resources.Clear();
        ServiceResourceChoices.Clear();
        foreach (var r in (await catalog.GetResourcesAsync(includeInactive: true)).OrEmpty())
        {
            Resources.Add(r);
            if (r.IsActive)
            {
                ServiceResourceChoices.Add(new ResourceChoice(r));
            }
        }

        Templates.Clear();
        if (ShowPackages)
        {
            foreach (var t in (await catalog.GetPackageTemplatesAsync(includeInactive: true)).OrEmpty())
            {
                Templates.Add(t);
            }
        }
    }
}
