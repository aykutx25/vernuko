using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation;

public enum SetupStep
{
    Welcome,
    Profile,
    Business,
    WorkingDays,
    WorkingHours,
    Staff,
    Services,
    Admin,
    Backup,
    Finish,
}

public sealed partial class ProfileOption(BusinessProfileDefinition profile) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public BusinessProfileDefinition Profile { get; } = profile;

    public string Name => Profile.DisplayName;

    public string Description => Profile.Description;
}

public sealed partial class DayOption(DayOfWeek day, bool isOpen) : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen = isOpen;

    public DayOfWeek Day { get; } = day;

    public string Name => DayNames.Turkish(Day);
}

public sealed partial class EditableStaff : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _title;
}

public sealed partial class EditableService : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _durationMinutes = "30";

    [ObservableProperty]
    private string _price = "0";

    [ObservableProperty]
    private string _cleanupMinutes = "0";

    public string? Category { get; init; }

    public ResourceKind? RequiredResource { get; init; }

    /// <summary>Rooms/devices whose name contains one of these words are linked to the service at setup.</summary>
    public IReadOnlyList<string>? ResourceKeywords { get; init; }
}

public sealed partial class EditableResource : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ResourceKind _kind = ResourceKind.Room;

    public string KindName => BusinessProfileCatalog.ResourceKindName(Kind);

    partial void OnKindChanged(ResourceKind value) => OnPropertyChanged(nameof(KindName));
}

/// <summary>
/// First-run wizard. Only business name, profile and an admin account are mandatory; staff, services and backup
/// steps can be skipped (sensible defaults are used and can be changed later in Settings).
/// </summary>
public sealed partial class SetupWizardViewModel : ViewModelBase
{
    private readonly SetupService _setup;
    private readonly IAuthService _auth;
    private readonly IDialogService _dialogs;
    private readonly IPlatformServices _platform;
    private readonly IBackupService _backups;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepIndex), nameof(StepTitle), nameof(CanGoBack), nameof(CanSkip), nameof(IsLastInputStep), nameof(Progress))]
    private SetupStep _step = SetupStep.Welcome;

    [ObservableProperty]
    private ProfileOption? _selectedProfile;

    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string _openTime = "09:00";

    [ObservableProperty]
    private string _closeTime = "19:00";

    [ObservableProperty]
    private bool _hasBreak = true;

    [ObservableProperty]
    private string _breakStart = "12:30";

    [ObservableProperty]
    private string _breakEnd = "13:30";

    [ObservableProperty]
    private string _adminDisplayName = string.Empty;

    [ObservableProperty]
    private string _adminUsername = string.Empty;

    [ObservableProperty]
    private string _adminPassword = string.Empty;

    [ObservableProperty]
    private string _adminPasswordConfirm = string.Empty;

    [ObservableProperty]
    private bool _enableBackup = true;

    [ObservableProperty]
    private string _backupFolder;

    [ObservableProperty]
    private string? _recoveryKey;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartUsingCommand))]
    private bool _recoveryKeyWrittenDown;

    [ObservableProperty]
    private bool _isCompleted;

    public SetupWizardViewModel(SetupService setup, IAuthService auth, IDialogService dialogs, IPlatformServices platform, IAppEnvironment env, IBackupService backups)
    {
        _backups = backups;
        _setup = setup;
        _auth = auth;
        _dialogs = dialogs;
        _platform = platform;
        _backupFolder = env.DefaultBackupDirectory;
        Profiles = new ObservableCollection<ProfileOption>(BusinessProfileCatalog.All.Where(p => p.IsAvailable).Select(p => new ProfileOption(p)));
        Days = new ObservableCollection<DayOption>(DayNames.WeekOrder.Select(d => new DayOption(d, d != DayOfWeek.Sunday)));
        Staff = [new EditableStaff()];
        ResourceKinds = Enum.GetValues<ResourceKind>().Select(k => new Option<ResourceKind>(BusinessProfileCatalog.ResourceKindName(k), k)).ToList();
    }

    public event EventHandler? Completed;

    /// <summary>Raised after a backup from another computer was restored instead of running the wizard.</summary>
    public event EventHandler? RestoredFromBackup;

    public ObservableCollection<ProfileOption> Profiles { get; }

    public ObservableCollection<DayOption> Days { get; }

    public ObservableCollection<EditableStaff> Staff { get; }

    public ObservableCollection<EditableService> Services { get; } = [];

    public ObservableCollection<EditableResource> Resources { get; } = [];

    public IReadOnlyList<Option<ResourceKind>> ResourceKinds { get; }

    public int StepIndex => (int)Step + 1;

    public static int StepCount => Enum.GetValues<SetupStep>().Length;

    public double Progress => StepIndex * 100.0 / StepCount;

    public bool CanGoBack => Step is > SetupStep.Welcome and < SetupStep.Finish;

    public bool CanSkip => Step is SetupStep.Staff or SetupStep.Services or SetupStep.Backup;

    public bool IsLastInputStep => Step == SetupStep.Backup;

    public string StepTitle => Step switch
    {
        SetupStep.Welcome => "Hoş geldiniz",
        SetupStep.Profile => "İşletmenizin faaliyet alanı",
        SetupStep.Business => "İşletme bilgileri",
        SetupStep.WorkingDays => "Çalışma günleri",
        SetupStep.WorkingHours => "Çalışma saatleri",
        SetupStep.Staff => "Personel",
        SetupStep.Services => "Hizmetler ve odalar",
        SetupStep.Admin => "Yönetici hesabı",
        SetupStep.Backup => "Yedekleme",
        _ => "Kurulum tamamlandı",
    };

    public string StepHint => Step switch
    {
        SetupStep.Welcome => "Vernuko'yu birkaç dakikada kullanıma hazırlayalım. Zorunlu olmayan adımları atlayıp daha sonra Ayarlar'dan tamamlayabilirsiniz.",
        SetupStep.Profile => "Seçiminize göre ekranlar, hizmet önerileri ve kullanılan terimler ayarlanır. Daha sonra değiştirilebilir.",
        SetupStep.Business => "İşletme adınız ekranlarda ve müşteri bildirimlerinde görünür.",
        SetupStep.WorkingDays => "Kapalı günlere randevu verilirken uyarı gösterilir.",
        SetupStep.WorkingHours => "Personel bazında farklı saatleri daha sonra tanımlayabilirsiniz.",
        SetupStep.Staff => "Randevu alacak uzmanları ekleyin. Atlarsanız yönetici adıyla bir personel oluşturulur.",
        SetupStep.Services => "Önerilen hizmetleri düzenleyebilir, fiyat ve süreleri değiştirebilirsiniz.",
        SetupStep.Admin => "Bu hesap tüm yetkilere sahiptir. Parolanızı kimseyle paylaşmayın.",
        SetupStep.Backup => "Yedekler bilgisayarınızda şifrelenir. Mümkünse harici bir diske veya USB belleğe yedekleyin.",
        _ => string.Empty,
    };

    [RelayCommand]
    private void Next()
    {
        ErrorMessage = null;
        var error = ValidateStep(Step);
        if (error is not null)
        {
            ErrorMessage = error;
            return;
        }

        if (Step == SetupStep.Profile)
        {
            ApplyProfileDefaults();
        }

        if (Step == SetupStep.Backup)
        {
            FinishCommand.Execute(null);
            return;
        }

        Step++;
    }

    [RelayCommand]
    private void Back()
    {
        ErrorMessage = null;
        if (CanGoBack)
        {
            Step--;
        }
    }

    [RelayCommand]
    private void Skip()
    {
        ErrorMessage = null;
        switch (Step)
        {
            case SetupStep.Staff:
                Staff.Clear();
                break;
            case SetupStep.Services:
                ApplyProfileDefaults(force: true);
                break;
            case SetupStep.Backup:
                EnableBackup = false;
                FinishCommand.Execute(null);
                return;
        }

        Step++;
    }

    [RelayCommand]
    private void SelectProfile(ProfileOption option)
    {
        foreach (var p in Profiles)
        {
            p.IsSelected = p == option;
        }

        SelectedProfile = option;
    }

    [RelayCommand]
    private void AddStaff() => Staff.Add(new EditableStaff());

    [RelayCommand]
    private void RemoveStaff(EditableStaff staff) => Staff.Remove(staff);

    [RelayCommand]
    private void AddService() => Services.Add(new EditableService { DurationMinutes = "30" });

    [RelayCommand]
    private void RemoveService(EditableService service) => Services.Remove(service);

    [RelayCommand]
    private void AddResource() => Resources.Add(new EditableResource { Kind = SelectedProfile?.Profile.ResourceKinds.FirstOrDefault() ?? ResourceKind.Room });

    [RelayCommand]
    private void RemoveResource(EditableResource resource) => Resources.Remove(resource);

    [RelayCommand]
    private async Task BrowseBackupFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Yedek klasörünü seçin", BackupFolder);
        if (folder is not null)
        {
            BackupFolder = folder;
        }
    }

    [RelayCommand]
    private void CopyRecoveryKey()
    {
        if (RecoveryKey is not null)
        {
            _platform.CopyToClipboard(RecoveryKey);
            InfoMessage = "Kurtarma anahtarı panoya kopyalandı. Bir kâğıda yazdıktan sonra panodan silmeyi unutmayın.";
        }
    }

    [RelayCommand]
    private Task FinishAsync() => RunAsync(async () =>
    {
        foreach (var step in new[] { SetupStep.Profile, SetupStep.Business, SetupStep.WorkingDays, SetupStep.WorkingHours, SetupStep.Admin })
        {
            var error = ValidateStep(step);
            if (error is not null)
            {
                ErrorMessage = error;
                Step = step;
                return;
            }
        }

        var request = BuildRequest();
        var validation = SetupService.Validate(request);
        if (!Check(validation))
        {
            return;
        }

        var result = await _setup.CompleteAsync(request);
        if (!Check(result))
        {
            return;
        }

        RecoveryKey = result.Value.RecoveryKey;
        Step = SetupStep.Finish;
        if (RecoveryKey is null)
        {
            RecoveryKeyWrittenDown = true;
        }
    });

    /// <summary>
    /// Moving to a new computer: restore the encrypted backup (with the recovery key) instead of setting up again.
    /// Users, customers, packages, payments and photos come back; sign in with the old username and password.
    /// </summary>
    [RelayCommand]
    private Task RestoreFromBackupAsync() => RunAsync(async () =>
    {
        var path = await _dialogs.PickOpenFileAsync("Eski bilgisayardan aldığınız yedeği seçin", "Vernuko yedeği|*.vbk");
        if (path is null)
        {
            return;
        }

        var info = await BackupService.DescribeAsync(path, null, CancellationToken.None);
        if (info.Problem is not null)
        {
            ErrorMessage = info.Problem;
            return;
        }

        var key = await _dialogs.PromptAsync("Kurtarma anahtarı", "Yedeklemeyi açarken size verilen kurtarma anahtarını girin (tireler isteğe bağlı):");
        if (key is null)
        {
            return;
        }

        var when = info.CreatedAtUtc is { } c ? $"{Formatting.ShortDate(c.ToLocalTime())} {Formatting.Time(c.ToLocalTime())}" : "bilinmeyen tarih";
        if (!await _dialogs.ConfirmAsync("Yedekten geri yükle",
                $"{when} tarihli yedek bu bilgisayara yüklenecek. Ardından eski bilgisayardaki kullanıcı adı ve parolanızla giriş yapacaksınız. Devam edilsin mi?",
                "Geri yükle"))
        {
            return;
        }

        if (Check(await _backups.RestoreIntoNewInstallationAsync(path, key)))
        {
            IsCompleted = true;
            RestoredFromBackup?.Invoke(this, EventArgs.Empty);
        }
    });

    [RelayCommand(CanExecute = nameof(CanStartUsing))]
    private Task StartUsingAsync() => RunAsync(async () =>
    {
        var login = await _auth.LoginAsync(AdminUsername, AdminPassword);
        AdminPassword = string.Empty;
        AdminPasswordConfirm = string.Empty;
        IsCompleted = true;
        if (Check(login))
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    });

    private bool CanStartUsing() => RecoveryKeyWrittenDown;

    public SetupRequest BuildRequest()
    {
        var open = Formatting.ParseTime(OpenTime) ?? new TimeOnly(9, 0);
        var close = Formatting.ParseTime(CloseTime) ?? new TimeOnly(19, 0);
        TimeInterval? breakInterval = HasBreak && Formatting.ParseTime(BreakStart) is { } bs && Formatting.ParseTime(BreakEnd) is { } be ? new TimeInterval(bs, be) : null;
        var hours = WeeklySchedule.Create(Days.Where(d => d.IsOpen).Select(d => d.Day), open, close, breakInterval);
        var staff = Staff.Where(s => !string.IsNullOrWhiteSpace(s.Name)).Select(s => new SetupStaff(s.Name.Trim(), s.Title)).ToList();
        if (staff.Count == 0)
        {
            staff.Add(new SetupStaff(string.IsNullOrWhiteSpace(AdminDisplayName) ? "Uzman" : AdminDisplayName.Trim(), null));
        }

        var services = Services.Where(s => !string.IsNullOrWhiteSpace(s.Name)).Select(s => new SetupServiceItem(
            s.Name.Trim(),
            s.Category,
            int.TryParse(s.DurationMinutes, out var d) ? d : 30,
            Money.Parse(s.Price) is { IsSuccess: true } m ? m.Value.Minor : 0,
            0,
            int.TryParse(s.CleanupMinutes, out var c) ? c : 0,
            s.RequiredResource,
            s.ResourceKeywords)).ToList();
        var resources = Resources.Where(r => !string.IsNullOrWhiteSpace(r.Name)).Select(r => new SetupResource(r.Name.Trim(), r.Kind)).ToList();
        return new SetupRequest(
            SelectedProfile?.Profile.Id ?? BusinessProfileId.General,
            BusinessName.Trim(),
            Phone,
            Address,
            TimeZones.Turkey,
            hours,
            staff,
            services,
            resources,
            AdminDisplayName.Trim(),
            AdminUsername.Trim(),
            AdminPassword,
            EnableBackup,
            EnableBackup ? BackupFolder : null);
    }

    private string? ValidateStep(SetupStep step)
    {
        switch (step)
        {
            case SetupStep.Profile when SelectedProfile is null:
                return "Lütfen işletmenizin faaliyet alanını seçin.";
            case SetupStep.Business when string.IsNullOrWhiteSpace(BusinessName):
                return "İşletme adı zorunludur.";
            case SetupStep.Business when !string.IsNullOrWhiteSpace(Phone) && PhoneNumber.Parse(Phone).IsFailure:
                return PhoneNumber.Parse(Phone).Error.Message;
            case SetupStep.WorkingDays when !Days.Any(d => d.IsOpen):
                return "En az bir çalışma günü seçin.";
            case SetupStep.WorkingHours:
            {
                var open = Formatting.ParseTime(OpenTime);
                var close = Formatting.ParseTime(CloseTime);
                if (open is null || close is null || close <= open)
                {
                    return "Açılış ve kapanış saatlerini SS:DD biçiminde girin (ör. 09:00 – 19:00).";
                }

                if (HasBreak)
                {
                    var bs = Formatting.ParseTime(BreakStart);
                    var be = Formatting.ParseTime(BreakEnd);
                    if (bs is null || be is null || be <= bs || bs <= open || be >= close)
                    {
                        return "Öğle arası, çalışma saatleri içinde olmalıdır.";
                    }
                }

                return null;
            }

            case SetupStep.Services:
                foreach (var s in Services.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
                {
                    if (!int.TryParse(s.DurationMinutes, out var d) || d is < 5 or > 1440)
                    {
                        return $"\"{s.Name}\" için süre 5–1440 dakika olmalıdır.";
                    }

                    if (Money.Parse(s.Price).IsFailure)
                    {
                        return $"\"{s.Name}\" için fiyat geçersiz.";
                    }
                }

                return null;
            case SetupStep.Admin:
            {
                if (string.IsNullOrWhiteSpace(AdminDisplayName) || string.IsNullOrWhiteSpace(AdminUsername))
                {
                    return "Ad soyad ve kullanıcı adı zorunludur.";
                }

                if (AdminUsername.Trim().Length < 3 || AdminUsername.Any(char.IsWhiteSpace))
                {
                    return "Kullanıcı adı en az 3 karakter olmalı ve boşluk içermemelidir.";
                }

                var policy = Vernuko.Security.PasswordPolicy.Validate(AdminPassword, AdminUsername);
                if (policy.IsFailure)
                {
                    return policy.Error.Message;
                }

                return AdminPassword != AdminPasswordConfirm ? "Parolalar eşleşmiyor." : null;
            }

            case SetupStep.Backup when EnableBackup && string.IsNullOrWhiteSpace(BackupFolder):
                return "Yedek klasörü seçin veya yedeklemeyi şimdilik atlayın.";
        }

        return null;
    }

    private void ApplyProfileDefaults(bool force = false)
    {
        if (SelectedProfile is null || (!force && Services.Count > 0 && Services[0].Category is not null && _appliedProfile == SelectedProfile.Profile.Id))
        {
            return;
        }

        _appliedProfile = SelectedProfile.Profile.Id;
        Services.Clear();
        foreach (var s in SelectedProfile.Profile.SuggestedServices)
        {
            Services.Add(new EditableService
            {
                Name = s.Name,
                DurationMinutes = s.DurationMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CleanupMinutes = s.CleanupMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Price = "0",
                Category = s.Category,
                RequiredResource = s.RequiredResource,
                ResourceKeywords = s.ResourceKeywords,
            });
        }

        Resources.Clear();
        foreach (var r in SelectedProfile.Profile.SuggestedResources)
        {
            Resources.Add(new EditableResource { Name = r.Name, Kind = r.Kind });
        }
    }

    private BusinessProfileId? _appliedProfile;
}
