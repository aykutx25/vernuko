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
    IPlatformServices platform) : PageViewModel, IShellAware
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

    public ObservableCollection<AuditEntryDto> AuditEntries { get; } = [];

    public IReadOnlyList<Option<SystemRole>> Roles { get; } = Enum.GetValues<SystemRole>().Where(r => r != SystemRole.Custom).Select(r => new Option<SystemRole>(RoleDefaults.DisplayName(r), r)).ToList();

    public IReadOnlyList<Option<BackupSchedule>> Schedules { get; } =
    [
        new("Kapalı", BackupSchedule.Off), new("Günlük", BackupSchedule.Daily), new("Haftalık", BackupSchedule.Weekly), new("Aylık", BackupSchedule.Monthly),
    ];

    /// <summary>Cost transparency: every paid integration is off by default and nothing is charged without opt-in.</summary>
    public IReadOnlyList<IntegrationRow> Integrations { get; } =
    [
        new("Yerel şifreli yedek (bilgisayar / USB)", "Aktif", "Ücretsiz", "Yedekler sizin diskinizde, AES-256 ile şifreli.", true),
        new("WhatsApp — manuel gönderim", "Aktif", "Ücretsiz", "Mesaj WhatsApp'ta açılır, siz kontrol edip gönderirsiniz.", true),
        new("WhatsApp Business Cloud API", "Kapalı", "0 ₺ — aylık limit 0 ₺", "Faz 5: kendi Meta hesabınızla, onaylı şablonlarla. Henüz mevcut değil.", false),
        new("SMS (Netgsm / İleti Merkezi / Verimor)", "Kapalı", "0 ₺ — aylık limit 0 ₺", "Faz 5: kendi sağlayıcı hesabınızla. Henüz mevcut değil.", false),
        new("Google Drive / OneDrive / Dropbox yedek", "Kapalı", "Kendi bulut kotanız", "Faz 4: kendi hesabınızda, buluta gitmeden önce şifreli. Henüz mevcut değil.", false),
        new("E-posta (SMTP)", "Kapalı", "—", "Planlanıyor.", false),
    ];

    public bool CanManageSettings => Shell?.Has(Permission.SettingsManage) ?? false;

    public bool CanManageUsers => Shell?.Has(Permission.UsersManage) ?? false;

    public bool CanManageBackups => Shell?.Has(Permission.BackupsManage) ?? false;

    public bool CanRestore => Shell?.Has(Permission.BackupsRestore) ?? false;

    public bool CanViewAudit => Shell?.Has(Permission.AuditLogView) ?? false;

    public bool CanManageLicense => Shell?.Has(Permission.LicenseManage) ?? false;

    public string DeviceId => device.DeviceHash;

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
            InfoMessage = $"Şifreli yedek oluşturuldu: {r.Value.FileName} ({r.Value.SizeBytes / 1024:N0} KB, {r.Value.Duration.TotalSeconds:0.0} sn).";
            await LoadBackupAsync();
            if (Shell is not null)
            {
                await Shell.RefreshStatusAsync();
            }
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
            InfoMessage = $"Yedek sağlam: {Formatting.ShortDate(r.Value.CreatedAtUtc.ToLocalTime())} {Formatting.Time(r.Value.CreatedAtUtc.ToLocalTime())}, veritabanı {r.Value.DatabaseBytes / 1024:N0} KB, bütünlük kontrolü başarılı. Fotoğraf/ek dosya içermez.";
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
