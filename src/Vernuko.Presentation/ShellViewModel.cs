using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Licensing;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Dialogs;
using Vernuko.Presentation.Pages;
using Vernuko.Security;

namespace Vernuko.Presentation;

public sealed partial class NavItem(string key, string title, string icon, Type pageType, Permission? permission) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string Key { get; } = key;

    public string Title { get; } = title;

    /// <summary>Resource key of the icon geometry in the WPF design system.</summary>
    public string Icon { get; } = icon;

    public Type PageType { get; } = pageType;

    public Permission? Permission { get; } = permission;
}

/// <summary>Main window chrome: navigation, global search, user menu, status bar and modal overlays.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly UserSession _session;
    private readonly ISettingsService _settings;
    private readonly ILicenseService _license;
    private readonly IBackupService _backups;
    private readonly IAuthService _auth;
    private readonly TimeProvider _time;
    private readonly IAppEnvironment _env;
    private readonly Dictionary<Type, PageViewModel> _pages = [];

    [ObservableProperty]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private ViewModelBase? _overlay;

    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _globalSearchText = string.Empty;

    [ObservableProperty]
    private string _licenseText = string.Empty;

    [ObservableProperty]
    private bool _licenseReadOnly;

    [ObservableProperty]
    private string _backupStatusText = string.Empty;

    [ObservableProperty]
    private bool _backupNeedsAttention;

    [ObservableProperty]
    private string _cloudBackupText = "Bulut yedekleme: Kapalı";

    [ObservableProperty]
    private bool _isDemo;

    /// <summary>"Hatırlatma: SMS" / "Hatırlatma: kapalı"; empty (hidden) for users who cannot see the setting.</summary>
    [ObservableProperty]
    private string _messagingStatusText = string.Empty;

    [ObservableProperty]
    private string _connectionText = "Yerel veritabanı — internetsiz çalışır";

    [ObservableProperty]
    private BusinessInfo? _business;

    public ShellViewModel(IServiceProvider services, UserSession session, ISettingsService settings, ILicenseService license, IBackupService backups, IAuthService auth, TimeProvider time, IAppEnvironment env)
    {
        _env = env;
        _services = services;
        _session = session;
        _settings = settings;
        _license = license;
        _backups = backups;
        _auth = auth;
        _time = time;
    }

    public ObservableCollection<NavItem> NavItems { get; } = [];

    public string UserDisplayName => _session.DisplayName ?? string.Empty;

    public string UserRoleName => _session.Role is { } r ? RoleDefaults.DisplayName(r) : string.Empty;

    public string UserInitials => Formatting.Initials(_session.DisplayName);

    public string VersionText => $"Sürüm {_env.AppVersion}";

    public event EventHandler? LoggedOut;

    public async Task InitializeAsync()
    {
        Business = await _settings.GetBusinessInfoAsync();
        BusinessName = Business?.BusinessName ?? "Vernuko";
        ProfileName = Business?.ProfileName ?? string.Empty;
        IsDemo = Business?.IsDemo == true;
        BuildNavigation();
        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(UserRoleName));
        OnPropertyChanged(nameof(UserInitials));
        await RefreshStatusAsync();
        var start = NavItems.FirstOrDefault(n => n.Key == "appointments") ?? NavItems.First();
        await NavigateAsync(start, null);
    }

    public async Task RefreshStatusAsync()
    {
        var computer = _services.GetService(typeof(IMultiComputer)) as IMultiComputer;
        ConnectionText = computer?.Role switch
        {
            ComputerRole.Client => "Ana bilgisayara bağlı" + (computer.Addresses.Count > 0 ? $" ({computer.Addresses[0]})" : string.Empty),
            ComputerRole.Host => "Ana bilgisayar — veriler bu bilgisayarda",
            _ => "Yerel veritabanı — internetsiz çalışır",
        };
        if (_session.Has(Permission.SettingsManage) && _services.GetService(typeof(IAutoMessagingService)) is IAutoMessagingService messaging
            && await messaging.GetSettingsAsync() is { IsSuccess: true } m)
        {
            MessagingStatusText = m.Value.RemindersEnabled
                ? $"Hatırlatma: {(m.Value.ReminderChannel == Messaging.MessageChannel.Sms ? "SMS" : "WhatsApp")}"
                : "Hatırlatma: kapalı";
        }
        else
        {
            MessagingStatusText = string.Empty;
        }

        var license = await _license.RefreshAsync();
        LicenseText = license.Message;
        LicenseReadOnly = license.Mode == AccessMode.ReadOnly;

        var backup = await _backups.GetStatusAsync();
        CloudBackupText = "Bulut yedekleme: " + backup.CloudText;
        var now = _time.GetUtcNow().UtcDateTime;
        if (!backup.KeyConfigured)
        {
            BackupStatusText = "Yedekleme kapalı";
            BackupNeedsAttention = true;
        }
        else if (backup.LastSuccessUtc is { } last)
        {
            var local = last.ToLocalTime();
            BackupStatusText = $"Son yedekleme: {(local.Date == DateTime.Today ? "Bugün" : Formatting.ShortDate(local))} {Formatting.Time(local)}";
            BackupNeedsAttention = now - last > TimeSpan.FromDays(7) || !string.IsNullOrEmpty(backup.LastError);
        }
        else
        {
            BackupStatusText = "Henüz yedek alınmadı";
            BackupNeedsAttention = true;
        }

        if (!string.IsNullOrEmpty(backup.LastError))
        {
            BackupStatusText += " · son deneme başarısız";
        }
    }

    [RelayCommand]
    private Task NavigateAsync(NavItem item) => NavigateAsync(item, null);

    public async Task NavigateAsync(NavItem item, object? parameter)
    {
        foreach (var n in NavItems)
        {
            n.IsSelected = n == item;
        }

        if (!_pages.TryGetValue(item.PageType, out var page))
        {
            page = (PageViewModel)_services.GetRequiredService(item.PageType);
            if (page is IShellAware aware)
            {
                aware.Shell = this;
            }

            _pages[item.PageType] = page;
        }

        CurrentPage = page;
        await page.OnNavigatedToAsync(parameter);
    }

    public Task NavigateAsync(string key, object? parameter = null)
    {
        var item = NavItems.FirstOrDefault(n => n.Key == key);
        return item is null ? Task.CompletedTask : NavigateAsync(item, parameter);
    }

    [RelayCommand]
    private Task GlobalSearchAsync() => NavigateAsync("customers", new CustomerSearchRequest(GlobalSearchText));

    [RelayCommand]
    private Task NewAppointmentAsync() => NavigateAsync("appointments", new OpenNewAppointment(null));

    [RelayCommand]
    private void Lock() => _auth.Lock();

    [RelayCommand]
    private void Logout()
    {
        _auth.Logout();
        _pages.Clear();
        CurrentPage = null;
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    public void ShowOverlay(ViewModelBase overlay) => Overlay = overlay;

    public void CloseOverlay() => Overlay = null;

    public bool Has(Permission permission) => _session.Has(permission);

    /// <summary>After the business settings were saved: name, auto-lock time etc. apply at once, not at the next login.</summary>
    public async Task ReloadBusinessAsync()
    {
        Business = await _settings.GetBusinessInfoAsync();
        BusinessName = Business?.BusinessName ?? BusinessName;
    }

    /// <summary>Whether the signed-in user has the page (quick-action tiles for missing pages are hidden).</summary>
    public bool HasPage(string key) => NavItems.Any(n => n.Key == key);

    /// <summary>
    /// Window-wide keyboard shortcuts. The view maps keys to <see cref="Shortcut"/> and calls this from the window,
    /// so shortcuts work wherever the keyboard focus is (including nowhere, e.g. right after login).
    /// Returns true when the shortcut was used. While a modal dialog is open only Esc (close it) is accepted.
    /// </summary>
    public bool HandleShortcut(Shortcut shortcut)
    {
        if (Overlay is not null)
        {
            if (shortcut != Shortcut.Close)
            {
                return false;
            }

            switch (Overlay)
            {
                case PaymentDialogViewModel payment:
                    payment.CancelCommand.Execute(null);
                    return true;
                case SellPackageDialogViewModel sale:
                    sale.CancelCommand.Execute(null);
                    return true;
                default:
                    return false;
            }
        }

        switch (shortcut)
        {
            case Shortcut.NewAppointment when Has(Permission.AppointmentsManage):
                NewAppointmentCommand.Execute(null);
                return true;
            case Shortcut.Lock:
                LockCommand.Execute(null);
                return true;
        }

        if (CurrentPage is not AppointmentsViewModel appointments)
        {
            return false;
        }

        var editor = appointments.Editor;
        switch (shortcut)
        {
            case Shortcut.Save when editor is not null:
                editor.SaveCommand.Execute(null);
                return true;
            case Shortcut.Close when editor is not null:
                editor.CancelCommand.Execute(null);
                return true;
            case Shortcut.Refresh:
                appointments.RefreshCommand.Execute(null);
                return true;
            case Shortcut.Previous when editor is null:
                appointments.PreviousPeriodCommand.Execute(null);
                return true;
            case Shortcut.Next when editor is null:
                appointments.NextPeriodCommand.Execute(null);
                return true;
            case Shortcut.Today when editor is null:
                appointments.TodayCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private void BuildNavigation()
    {
        NavItems.Clear();
        var terms = Business?.Terms ?? new Terminology("Müşteri", "Müşteriler", "Personel", "Personel", "Kaynak", "Kaynaklar");
        var features = Business?.Features ?? new HashSet<ProfileFeature>();
        var items = new List<NavItem>
        {
            new("home", "Ana Sayfa", "IconHome", typeof(HomeViewModel), Permission.AppointmentsView),
            new("appointments", "Randevular", "IconCalendar", typeof(AppointmentsViewModel), Permission.AppointmentsView),
            new("customers", terms.Customers, "IconUsers", typeof(CustomersViewModel), Permission.CustomersView),
            new("catalog", "Hizmetler", "IconLayers", typeof(CatalogViewModel), Permission.AppointmentsView),
            new("staff", terms.Staff, "IconStaff", typeof(StaffViewModel), Permission.AppointmentsView),
        };
        if (features.Contains(ProfileFeature.SessionPackages))
        {
            items.Add(new("packages", "Paket & Seanslar", "IconBox", typeof(PackagesViewModel), Permission.PackagesView));
        }

        if (features.Contains(ProfileFeature.Waitlist))
        {
            items.Add(new("waitlist", "Bekleme Listesi", "IconHourglass", typeof(WaitlistViewModel), Permission.AppointmentsView));
        }

        if (features.Contains(ProfileFeature.ProductStock))
        {
            items.Add(new("inventory", "Stok", "IconTag", typeof(InventoryViewModel), Permission.InventoryView));
        }

        items.Add(new("payments", "Ödemeler", "IconCard", typeof(PaymentsViewModel), Permission.PaymentsView));
        items.Add(new("reports", "Raporlar", "IconChart", typeof(ReportsViewModel), Permission.ReportsOperational));
        items.Add(new("settings", "Ayarlar", "IconSettings", typeof(SettingsViewModel), null));
        foreach (var item in items.Where(i => i.Permission is null || _session.Has(i.Permission.Value)))
        {
            NavItems.Add(item);
        }
    }
}

/// <summary>Pages that need to open overlays or navigate get a reference to the shell.</summary>
public interface IShellAware
{
    ShellViewModel? Shell { get; set; }
}

public enum Shortcut
{
    NewAppointment,
    Lock,
    Save,
    Close,
    Refresh,
    Previous,
    Next,
    Today,
}

public sealed record CustomerSearchRequest(string Query);

public sealed record OpenNewAppointment(Guid? CustomerId);

public sealed record OpenCustomer(Guid CustomerId);
