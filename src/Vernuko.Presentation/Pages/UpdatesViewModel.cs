using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

/// <summary>
/// Ayarlar › Güncellemeler. Before an update is applied an encrypted backup is taken automatically; the new version
/// migrates the database with its own safety copy. Nothing is downloaded without the user's click.
/// </summary>
public sealed partial class UpdatesViewModel(IUpdateService updates, BackupService backups, IDialogService dialogs) : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string? _newVersion;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private bool _isDownloading;

    public string CurrentVersion => updates.CurrentVersion;

    public string? UnavailableReason => updates.UnavailableReason;

    public bool CanCheck => updates.UnavailableReason is null;

    public void Load()
    {
        StatusText = CanCheck ? "Güncellemeleri denetlemek için düğmeye basın." : string.Empty;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private Task CheckAsync() => RunAsync(async () =>
    {
        var r = await updates.CheckAsync();
        UpdateAvailable = r.Available;
        NewVersion = r.NewVersion;
        StatusText = r.Message;
    });

    [RelayCommand]
    private Task InstallAsync() => RunAsync(async () =>
    {
        if (!UpdateAvailable)
        {
            return;
        }

        if (!await dialogs.ConfirmAsync("Güncelleme", $"Vernuko {NewVersion} sürümüne güncellenecek.\n\nÖnce verilerinizin şifreli yedeği alınır, sonra program kapanıp yeni sürümle açılır. Devam edilsin mi?", "Güncelle"))
        {
            return;
        }

        var status = await backups.GetStatusAsync();
        if (status.KeyConfigured)
        {
            var backup = await backups.CreateBackupAsync(BackupTrigger.PreUpdate);
            if (backup.IsFailure && !await dialogs.ConfirmAsync("Yedek alınamadı",
                    $"{backup.Error.Message}\n\nYedeksiz güncelleme ÖNERİLMEZ. Yine de devam edilsin mi?", "Yedeksiz devam et", destructive: true))
            {
                return;
            }
        }
        else if (!await dialogs.ConfirmAsync("Yedekleme kapalı", "Yedekleme açık olmadığı için güncelleme öncesi yedek alınamıyor. Önce Ayarlar › Yedekleme bölümünden yedeklemeyi açmanız önerilir. Yine de devam edilsin mi?", "Devam et", destructive: true))
        {
            return;
        }

        IsDownloading = true;
        try
        {
            var ok = await updates.DownloadAsync(new Progress<int>(p => Progress = p));
            if (!ok)
            {
                ErrorMessage = "Güncelleme indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin; mevcut sürüm çalışmaya devam eder.";
                return;
            }
        }
        finally
        {
            IsDownloading = false;
        }

        StatusText = "Güncelleme indirildi. Program yeniden başlatılıyor…";
        updates.ApplyAndRestart();
    });
}
