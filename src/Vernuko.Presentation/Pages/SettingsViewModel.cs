using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.Core;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

public sealed record IntegrationRow(string Name, string Status, string Cost, string Note, bool IsActive);

public sealed partial class SettingsViewModel(
    SettingsService settings,
    UserAdminService users,
    AuthService auth,
    BackupService backups,
    LicenseService license,
    AuditService audit,
    IDeviceIdentity device,
    IDialogService dialogs,
    IPlatformServices platform,
    CloudFolderLocator cloudLocator,
    CommissionRulesViewModel commissionRules,
    ConsentTemplatesViewModel consentTemplates,
    UpdatesViewModel updates,
    MessagingSettingsViewModel messaging) : PageViewModel, IShellAware
{
    // Business
    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string _slotMinutes = "15";

    [ObservableProperty]
    private bool _noShowConsumesSession;

    [ObservableProperty]
    private bool _autoConfirm;

    [ObservableProperty]
    private string _autoLockMinutes = "10";

    [ObservableProperty]
    private string _profileName = string.Empty;

    // Users
    [ObservableProperty]
    private UserSummary? _selectedUser;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newUserDisplayName = string.Empty;

    [ObservableProperty]
    private string _newUserPassword = string.Empty;

    [ObservableProperty]
    private Option<SystemRole>? _newUserRole;

    // Backup
    [ObservableProperty]
    private BackupStatus? _backupStatus;

    [ObservableProperty]
    private string _backupFolder = string.Empty;

    [ObservableProperty]
    private Option<BackupSchedule>? _schedule;

    [ObservableProperty]
    private bool _backupOnExit = true;

    [ObservableProperty]
    private string _retainDaily = "7";

    [ObservableProperty]
    private string _retainWeekly = "4";

    [ObservableProperty]
    private string _retainMonthly = "3";

    [ObservableProperty]
    private BackupListItem? _selectedBackup;

    [ObservableProperty]
    private string? _shownRecoveryKey;

    // Cloud copy
    [ObservableProperty]
    private CloudFolderCandidate? _selectedCloud;

    [ObservableProperty]
    private bool _cloudConsent;

    [ObservableProperty]
    private string _cloudStatusText = string.Empty;

    // License
    [ObservableProperty]
    private string _licenseText = string.Empty;

    // Account
    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _auditVerification = string.Empty;

    public override string Title => "Ayarlar";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<DayHoursRow> Hours { get; } = new(DayNames.WeekOrder.Select(d => new DayHoursRow(d)));

    public ObservableCollection<UserSummary> Users { get; } = [];

    public ObservableCollection<BackupListItem> Backups { get; } = [];

    public ObservableCollection<CloudFolderCandidate> CloudCandidates { get; } = [];

    public bool CloudEnabled => BackupStatus?.CloudEnabled == true;

    public bool HasNoCloudCandidates => CloudCandidates.Count == 0;

    public string CloudConsentText =>
        "Okudum, anlıyorum: yedek dosyası (şifreli) seçtiğim bulut hizmetinin sunucularına, yurt dışında olabilecek bir yere gönderilecek. " +
        "Bulut sağlayıcısı dosyanın içeriğini okuyamaz, ancak KVKK açısından bu bir yurt dışı aktarım sayılabilir; işletmem bu konuda kendi hukuki değerlendirmesini yapmalıdır.";

    public ObservableCollection<AuditEntryDto> AuditEntries { get; } = [];

    public IReadOnlyList<Option<SystemRole>> Roles { get; } = Enum.GetValues<SystemRole>().Where(r => r != SystemRole.Custom).Select(r => new Option<SystemRole>(RoleDefaults.DisplayName(r), r)).ToList();

    public IReadOnlyList<Option<BackupSchedule>> Schedules { get; } =
    [
        new("Kapalı", BackupSchedule.Off), new("Günlük", BackupSchedule.Daily), new("Haftalık", BackupSchedule.Weekly), new("Aylık", BackupSchedule.Monthly),
    ];

    /// <summary>Cost transparency: every paid integration is off by default and nothing is charged without opt-in.</summary>
    public IReadOnlyList<IntegrationRow> Integrations =>
    [
        new("Yerel şifreli yedek (bilgisayar / USB)", BackupStatus?.KeyConfigured == true ? "Aktif" : "Kapalı", "Ücretsiz", "Yedekler sizin diskinizde, AES-256 ile şifreli.", BackupStatus?.KeyConfigured == true),
        new("WhatsApp — manuel gönderim", "Aktif", "Ücretsiz", "Mesaj WhatsApp'ta açılır, siz kontrol edip gönderirsiniz.", true),
        MessagingRow(Vernuko.Messaging.MessageChannel.WhatsApp, "WhatsApp Business Cloud API (otomatik hatırlatma)", "Kendi Meta hesabınız ve onaylı şablonunuzla."),
        MessagingRow(Vernuko.Messaging.MessageChannel.Sms, "SMS — İleti Merkezi (otomatik hatırlatma)", "Kendi İleti Merkezi hesabınız ve onaylı başlığınızla. Netgsm ve Verimor henüz yok."),
        new("Bulut kopyası (OneDrive / Google Drive / Dropbox klasörü)", CloudEnabled ? "Aktif" : "Kapalı", "Kendi bulut kotanız",
            "Şifreli yedek, bilgisayarınızdaki bulut klasörüne kopyalanır; yüklemeyi bulut uygulamanız yapar. Ayarlar › Yedekleme'den açılır.", CloudEnabled),
        new("E-posta (SMTP)", "Kapalı", "—", "Planlanıyor.", false),
    ];

    public bool CanManageSettings => Shell?.Has(Permission.SettingsManage) ?? false;

    private IntegrationRow MessagingRow(Vernuko.Messaging.MessageChannel channel, string name, string note)
    {
        var s = Messaging.Current;
        var active = s is not null && s.RemindersEnabled && s.ReminderChannel == channel && s.IsConfigured(channel) && s.MonthlyLimit > 0;
        var cost = s is null ? "—" : $"Kendi hesabınız · aylık limit {s.MonthlyLimit} mesaj";
        return new IntegrationRow(name, active ? "Aktif" : "Kapalı", cost, note + " Ayarlar › Mesajlaşma.", active);
    }

    public bool CanManageUsers => Shell?.Has(Permission.UsersManage) ?? false;

    public bool CanManageBackups => Shell?.Has(Permission.BackupsManage) ?? false;

    public bool CanRestore => Shell?.Has(Permission.BackupsRestore) ?? false;

    public bool CanViewAudit => Shell?.Has(Permission.AuditLogView) ?? false;

    public bool CanManageLicense => Shell?.Has(Permission.LicenseManage) ?? false;

    public string DeviceId => device.DeviceHash;

    /// <summary>AGPL-3.0 §6: users who receive the program must be able to get its source code.</summary>
    public const string SourceCodeUrl = "https://github.com/aykutx25/vernuko";

    public string SourceCodeText => SourceCodeUrl;

    public CommissionRulesViewModel CommissionRules { get; } = commissionRules;

    public ConsentTemplatesViewModel ConsentTemplates { get; } = consentTemplates;

    public UpdatesViewModel Updates { get; } = updates;

    public MessagingSettingsViewModel Messaging { get; } = messaging;

    /// <summary>Commission rules and consent forms are useful for every profile (KVKK açık rıza applies everywhere).</summary>
    public bool ShowCommissionRules => CanManageSettings;

    public bool ShowConsentTemplates => CanManageSettings;

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        NewUserRole = Roles.First(r => r.Value == SystemRole.Secretary);
        await LoadBusinessAsync();
        await LoadUsersAsync();
        await LoadBackupAsync();
        LicenseText = (await license.RefreshAsync()).Message;
        if (CanViewAudit)
        {
            await LoadAuditAsync();
        }

        if (ShowCommissionRules)
        {
            await CommissionRules.LoadAsync();
        }

        if (ShowConsentTemplates)
        {
            await ConsentTemplates.LoadAsync();
        }

        Updates.Load();
        if (CanManageSettings)
        {
            await Messaging.LoadAsync();
        }

        OnPropertyChanged(string.Empty);
    });

    [RelayCommand]
    private Task SaveBusinessAsync() => RunAsync(async () =>
    {
        var hours = new WeeklySchedule();
        foreach (var row in Hours.Where(h => h.IsWorking))
        {
            if (Formatting.ParseTime(row.Start) is not { } s || Formatting.ParseTime(row.End) is not { } e || e <= s)
            {
                ErrorMessage = $"{row.Name} için saatleri SS:DD biçiminde girin.";
                return;
            }

            hours.Days[row.Day] = [new TimeInterval(s, e)];
        }

        // Keep existing breaks (lunch) when the open/close times did not change.
        var current = await settings.GetBusinessInfoAsync();
        if (current is not null)
        {
            foreach (var (day, intervals) in current.BusinessHours.Days)
            {
                if (hours.Days.TryGetValue(day, out var updated) && intervals.Count > 1 && updated[0].Start == intervals[0].Start && updated[0].End == intervals[^1].End)
                {
                    hours.Days[day] = intervals.ToList();
                }
            }
        }

        if (!int.TryParse(SlotMinutes, out var slot) || !int.TryParse(AutoLockMinutes, out var lockMinutes))
        {
            ErrorMessage = "Sayısal alanları kontrol edin.";
            return;
        }

        if (Check(await settings.UpdateBusinessAsync(BusinessName, Phone, Email, Address, hours, slot, NoShowConsumesSession, AutoConfirm, lockMinutes)))
        {
            InfoMessage = "İşletme ayarları kaydedildi.";
            if (Shell is not null)
            {
                Shell.BusinessName = BusinessName;
            }
        }
    });

    [RelayCommand]
    private Task AddUserAsync() => RunAsync(async () =>
    {
        if (Check(await users.CreateAsync(NewUsername, NewUserDisplayName, NewUserPassword, NewUserRole?.Value ?? SystemRole.Secretary)))
        {
            NewUsername = NewUserDisplayName = NewUserPassword = string.Empty;
            await LoadUsersAsync();
            InfoMessage = "Kullanıcı eklendi.";
        }
    });

    [RelayCommand]
    private Task ToggleUserActiveAsync(UserSummary user) => RunAsync(async () =>
    {
        if (Check(await users.UpdateAsync(user.Id, user.DisplayName, user.Role, !user.IsActive)))
        {
            await LoadUsersAsync();
        }
    });

    [RelayCommand]
    private Task ChangeUserRoleAsync(object? parameter) => RunAsync(async () =>
    {
        if (parameter is object[] { Length: 2 } args && args[0] is UserSummary user && args[1] is Option<SystemRole> role &&
            Check(await users.UpdateAsync(user.Id, user.DisplayName, role.Value, user.IsActive)))
        {
            await LoadUsersAsync();
        }
    });

    [RelayCommand]
    private Task ResetUserPasswordAsync(UserSummary user) => RunAsync(async () =>
    {
        var pw = await dialogs.PromptAsync("Parola sıfırla", $"{user.DisplayName} için yeni parola (en az 8 karakter):");
        if (pw is not null && Check(await users.ResetPasswordAsync(user.Id, pw)))
        {
            InfoMessage = "Parola sıfırlandı. Kullanıcıya güvenli bir yoldan iletin.";
        }
    });

    [RelayCommand]
    private Task ChangeOwnPasswordAsync() => RunAsync(async () =>
    {
        var r = await auth.ChangeOwnPasswordAsync(CurrentPassword, NewPassword);
        CurrentPassword = NewPassword = string.Empty;
        if (Check(r))
        {
            InfoMessage = "Parolanız değiştirildi.";
        }
    });

    [RelayCommand]
    private async Task BrowseBackupFolderAsync()
    {
        var folder = await dialogs.PickFolderAsync("Yedek klasörü", BackupFolder);
        if (folder is not null)
        {
            BackupFolder = folder;
        }
    }

    [RelayCommand]
    private Task SaveBackupSettingsAsync() => RunAsync(async () =>
    {
        if (!int.TryParse(RetainDaily, out var d) || !int.TryParse(RetainWeekly, out var w) || !int.TryParse(RetainMonthly, out var m))
        {
            ErrorMessage = "Saklama sayıları tam sayı olmalıdır.";
            return;
        }

        if (Check(await backups.ConfigureAsync(BackupFolder, Schedule?.Value ?? BackupSchedule.Daily, BackupOnExit, new RetentionPolicy(d, w, m))))
        {
            await LoadBackupAsync();
            InfoMessage = "Yedekleme ayarları kaydedildi.";
        }
    });

    [RelayCommand]
    private Task EnableBackupAsync() => RunAsync(async () =>
    {
        if (BackupStatus?.KeyConfigured == true && !await dialogs.ConfirmAsync("Yeni kurtarma anahtarı",
                "Yeni bir kurtarma anahtarı oluşturulacak. Eski yedekleri açmak için ESKİ anahtar gerekmeye devam eder. Devam edilsin mi?", "Yeni anahtar oluştur"))
        {
            return;
        }

        var key = await backups.CreateRecoveryKeyAsync();
        if (Check(key))
        {
            ShownRecoveryKey = key.Value;
            await LoadBackupAsync();
        }
    });

    [RelayCommand]
    private void CopyRecoveryKey()
    {
        if (ShownRecoveryKey is not null)
        {
            platform.CopyToClipboard(ShownRecoveryKey);
            InfoMessage = "Kopyalandı. Anahtarı kâğıda yazıp güvenli bir yerde saklayın.";
        }
    }

    [RelayCommand]
    private Task ConfirmRecoveryKeyAsync() => RunAsync(async () =>
    {
        var typed = await dialogs.PromptAsync("Kurtarma anahtarını doğrula", "Yazdığınız kurtarma anahtarını girin (tireler isteğe bağlı):");
        if (typed is not null && Check(await backups.ConfirmRecoveryKeyAsync(typed)))
        {
            ShownRecoveryKey = null;
            await LoadBackupAsync();
            InfoMessage = "Kurtarma anahtarı doğrulandı.";
        }
    });

    [RelayCommand]
    private Task BackupNowAsync() => RunAsync(async () =>
    {
        var r = await backups.CreateBackupAsync(BackupTrigger.Manual);
        if (Check(r))
        {
            InfoMessage = $"Şifreli yedek oluşturuldu: {r.Value.FileName} ({r.Value.SizeBytes / 1024:N0} KB, {r.Value.Duration.TotalSeconds:0.0} sn){(r.Value.CloudNote is { } note ? "; " + note : string.Empty)}.";
            await LoadBackupAsync();
            if (Shell is not null)
            {
                await Shell.RefreshStatusAsync();
            }
        }
    });

    [RelayCommand]
    private void DetectCloudFolders()
    {
        CloudCandidates.Clear();
        foreach (var c in cloudLocator.Detect())
        {
            CloudCandidates.Add(c);
        }

        SelectedCloud ??= CloudCandidates.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoCloudCandidates));
    }

    [RelayCommand]
    private async Task BrowseCloudFolderAsync()
    {
        var folder = await dialogs.PickFolderAsync("Bulut eşitleme klasörünü seçin", SelectedCloud?.Path);
        if (folder is not null)
        {
            var custom = new CloudFolderCandidate(CloudProvider.Other, folder);
            CloudCandidates.Add(custom);
            SelectedCloud = custom;
            OnPropertyChanged(nameof(HasNoCloudCandidates));
        }
    }

    [RelayCommand]
    private Task EnableCloudAsync() => RunAsync(async () =>
    {
        if (SelectedCloud is null)
        {
            ErrorMessage = "Önce bir bulut klasörü seçin.";
            return;
        }

        if (Check(await backups.EnableCloudCopyAsync(SelectedCloud.Path, SelectedCloud.Provider, CloudConsent)))
        {
            CloudConsent = false;
            await LoadBackupAsync();
            InfoMessage = $"Bulut kopyası açıldı. Bundan sonraki her yedek {SelectedCloud.ProviderName} klasörüne de kopyalanacak. Şimdi bir yedek alarak deneyebilirsiniz.";
        }
    });

    [RelayCommand]
    private Task DisableCloudAsync() => RunAsync(async () =>
    {
        if (Check(await backups.DisableCloudCopyAsync()))
        {
            await LoadBackupAsync();
            InfoMessage = "Bulut kopyası kapatıldı. Buluttaki eski kopyalar silinmedi.";
        }
    });

    [RelayCommand]
    private Task VerifyBackupAsync() => RunAsync(async () =>
    {
        if (SelectedBackup is null || BackupStatus is null)
        {
            return;
        }

        var path = Path.Combine(BackupStatus.Folder, SelectedBackup.FileName);
        var key = SelectedBackup.KeyMatches ? null : await dialogs.PromptAsync("Kurtarma anahtarı", "Bu yedek farklı bir anahtarla oluşturulmuş. Kurtarma anahtarını girin:");
        var r = await backups.VerifyAsync(path, key);
        if (Check(r))
        {
            InfoMessage = $"Yedek sağlam: {Formatting.ShortDate(r.Value.CreatedAtUtc.ToLocalTime())} {Formatting.Time(r.Value.CreatedAtUtc.ToLocalTime())}, veritabanı {r.Value.DatabaseBytes / 1024:N0} KB, bütünlük kontrolü başarılı.{(r.Value.AttachmentsIncluded ? " Müşteri fotoğrafları/dosyaları dahil." : string.Empty)}";
        }
    });

    [RelayCommand]
    private Task RestoreSelectedAsync() => SelectedBackup is null || BackupStatus is null
        ? Task.CompletedTask
        : RestoreFromAsync(Path.Combine(BackupStatus.Folder, SelectedBackup.FileName), SelectedBackup.KeyMatches);

    [RelayCommand]
    private async Task RestoreFromFileAsync()
    {
        var path = await dialogs.PickOpenFileAsync("Yedek dosyası seçin", "Vernuko yedeği|*.vbk");
        if (path is not null)
        {
            await RestoreFromAsync(path, keyMatches: false);
        }
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        if (BackupStatus is not null && Directory.Exists(BackupStatus.Folder))
        {
            platform.OpenFolder(BackupStatus.Folder);
        }
    }

    [RelayCommand]
    private Task ImportLicenseAsync() => RunAsync(async () =>
    {
        var path = await dialogs.PickOpenFileAsync("Lisans dosyası", "Vernuko lisansı|*.vlic;*.json");
        if (path is null)
        {
            return;
        }

        var r = await license.ImportAsync(await platform.ReadTextFileAsync(path));
        if (Check(r))
        {
            LicenseText = r.Value.Message;
            InfoMessage = "Lisans yüklendi.";
        }
    });

    [RelayCommand]
    private void OpenSourceCode() => platform.OpenUri(new Uri(SourceCodeUrl));

    /// <summary>Opens the program folder, which contains LICENSE.txt (AGPL-3.0) and THIRD-PARTY-NOTICES.md.</summary>
    [RelayCommand]
    private void ShowLicenseFiles() => platform.OpenFolder(AppContext.BaseDirectory);

    [RelayCommand]
    private Task VerifyAuditAsync() => RunAsync(async () =>
    {
        var r = await audit.VerifyChainAsync();
        if (Check(r))
        {
            AuditVerification = r.Value.IsIntact
                ? $"Denetim kaydı bütün: {r.Value.CheckedEntries} kayıt doğrulandı."
                : $"UYARI: Denetim kaydı #{r.Value.FirstBrokenSequence} numaralı kayıttan itibaren değiştirilmiş görünüyor.";
        }
    });

    private Task RestoreFromAsync(string path, bool keyMatches) => RunAsync(async () =>
    {
        var info = await BackupService.DescribeAsync(path, BackupStatus?.KeyId, CancellationToken.None);
        if (info.Problem is not null)
        {
            ErrorMessage = info.Problem;
            return;
        }

        var when = info.CreatedAtUtc is { } c ? $"{Formatting.ShortDate(c.ToLocalTime())} {Formatting.Time(c.ToLocalTime())}" : "bilinmeyen tarih";
        if (!await dialogs.ConfirmAsync("Yedekten geri yükle",
                $"Mevcut veriler {when} tarihli yedekteki haliyle DEĞİŞTİRİLECEK. Bu yedekten sonra girilen kayıtlar kaybolur.\n\nİşlemden önce mevcut verilerin güvenlik kopyası otomatik alınır. Devam edilsin mi?",
                "Geri yükle", destructive: true))
        {
            return;
        }

        string? key = null;
        if (!keyMatches && !info.KeyMatches)
        {
            key = await dialogs.PromptAsync("Kurtarma anahtarı", "Bu yedeği açmak için kurtarma anahtarını girin:");
            if (key is null)
            {
                return;
            }
        }

        var r = await backups.RestoreAsync(path, key);
        if (Check(r))
        {
            await dialogs.ShowMessageAsync("Geri yükleme tamamlandı", $"Veriler {when} tarihli yedekten geri yüklendi. Güvenlik kopyası: {r.Value.SafetyCopyPath}\n\nLütfen yeniden giriş yapın.");
            Shell?.LogoutCommand.Execute(null);
        }
    });

    private async Task LoadBusinessAsync()
    {
        var info = await settings.GetBusinessInfoAsync();
        if (info is null)
        {
            return;
        }

        BusinessName = info.BusinessName;
        Phone = info.Phone;
        Email = info.Email;
        Address = info.Address;
        ProfileName = info.ProfileName;
        SlotMinutes = info.SlotMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        NoShowConsumesSession = info.NoShowConsumesSession;
        AutoConfirm = info.AutoConfirmAppointments;
        AutoLockMinutes = info.AutoLockMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var row in Hours)
        {
            var intervals = info.BusinessHours.For(row.Day);
            row.IsWorking = intervals.Count > 0;
            if (intervals.Count > 0)
            {
                row.Start = intervals.Min(i => i.Start).ToString("HH:mm", TurkishText.Culture);
                row.End = intervals.Max(i => i.End).ToString("HH:mm", TurkishText.Culture);
            }
        }
    }

    private async Task LoadUsersAsync()
    {
        Users.Clear();
        if (!CanManageUsers)
        {
            return;
        }

        foreach (var u in (await users.ListAsync()).OrEmpty())
        {
            Users.Add(u);
        }
    }

    private async Task LoadBackupAsync()
    {
        BackupStatus = await backups.GetStatusAsync();
        BackupFolder = BackupStatus.Folder;
        Schedule = Schedules.FirstOrDefault(s => s.Value == BackupStatus.Schedule) ?? Schedules[1];
        BackupOnExit = BackupStatus.OnExit;
        RetainDaily = BackupStatus.Retention.Daily.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RetainWeekly = BackupStatus.Retention.Weekly.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RetainMonthly = BackupStatus.Retention.Monthly.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Backups.Clear();
        foreach (var b in await backups.ListBackupsAsync())
        {
            Backups.Add(b);
        }

        var s = BackupStatus;
        CloudStatusText = s.CloudFolder is null
            ? "Kapalı. Açarsanız her yedeğin şifreli bir kopyası bilgisayarınızdaki bulut klasörüne de konur."
            : $"Açık: {CloudFolderLocator.ProviderName(s.CloudProvider ?? CloudProvider.Other)} — {Path.Combine(s.CloudFolder, BackupService.CloudSubFolder)}"
              + (s.CloudLastSuccessUtc is { } ok ? $" · son kopya {Formatting.ShortDate(ok.ToLocalTime())} {Formatting.Time(ok.ToLocalTime())}" : " · henüz kopya yok")
              + (string.IsNullOrEmpty(s.CloudLastError) ? string.Empty : $" · SON DENEME BAŞARISIZ: {s.CloudLastError}");
        if (CloudCandidates.Count == 0)
        {
            DetectCloudFolders();
        }

        OnPropertyChanged(nameof(CloudEnabled));
        OnPropertyChanged(nameof(Integrations));
    }

    private async Task LoadAuditAsync()
    {
        AuditEntries.Clear();
        foreach (var e in (await audit.GetRecentAsync(200)).OrEmpty())
        {
            AuditEntries.Add(e);
        }
    }
}
