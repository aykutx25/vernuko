using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation;

/// <summary>Top-level state machine: setup wizard → login → main shell, with the lock screen as an overlay.</summary>
public sealed partial class RootViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly SetupService _setup;
    private readonly UserSession _session;
    private readonly BackupService _backups;

    [ObservableProperty]
    private ViewModelBase? _screen;

    [ObservableProperty]
    private LockViewModel? _lockScreen;

    public RootViewModel(IServiceProvider services, SetupService setup, UserSession session, BackupService backups)
    {
        _services = services;
        _setup = setup;
        _session = session;
        _backups = backups;
        _session.Changed += (_, _) => LockScreen = _session.IsAuthenticated && _session.IsLocked ? CreateLock() : null;
    }

    public ShellViewModel? Shell => Screen as ShellViewModel;

    /// <summary>The latest event-driven screen change (incl. a scheduled backup it started). Never faults.</summary>
    public Task LastScreenChange { get; private set; } = Task.CompletedTask;

    public async Task StartAsync()
    {
        if (!await _setup.IsCompletedAsync())
        {
            var wizard = _services.GetRequiredService<SetupWizardViewModel>();
            wizard.Completed += (_, _) => RunSafely(ShowShellAsync);
            wizard.RestoredFromBackup += (_, _) => RunSafely(() => ShowLoginAsync("Yedek bu bilgisayara yüklendi. Eski bilgisayardaki kullanıcı adı ve parolanızla giriş yapın."));
            Screen = wizard;
            return;
        }

        await ShowLoginAsync();
    }

    public Task ShowLoginAsync() => ShowLoginAsync(null);

    public async Task ShowLoginAsync(string? info)
    {
        var login = _services.GetRequiredService<LoginViewModel>();
        login.LoggedIn += (_, _) => RunSafely(ShowShellAsync);
        await login.LoadAsync();
        login.InfoMessage = info;
        Screen = login;
    }

    public async Task ShowShellAsync()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.LoggedOut += (_, _) => RunSafely(ShowLoginAsync);
        Screen = shell;
        await shell.InitializeAsync();
        await RunScheduledBackupIfDueAsync();
    }

    /// <summary>Automatic reminders (Phase 5): plan and send due messages. Never throws.</summary>
    public async Task RunMessagingAsync()
    {
        try
        {
            var messaging = _services.GetRequiredService<AutoMessagingService>();
            await messaging.PlanRemindersAsync();
            await messaging.DispatchDueAsync();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Automatic messaging run failed");
        }
    }

    /// <summary>License check-in with the license server (at most every 6 hours; offline is fine).</summary>
    public async Task RunLicenseCheckInAsync()
    {
        try
        {
            await _services.GetRequiredService<LicenseOnlineService>().AutoRefreshAsync();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "License check-in failed");
        }
    }

    /// <summary>Scheduled backups run only while the app is open — Vernuko never pretends to back up a switched-off PC.</summary>
    public async Task RunScheduledBackupIfDueAsync()
    {
        try
        {
            if (await _backups.IsScheduledBackupDueAsync())
            {
                await _backups.CreateBackupAsync(BackupTrigger.Scheduled);
                if (Shell is { } shell)
                {
                    await shell.RefreshStatusAsync();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // The app is shutting down while the backup was running; nothing to report.
        }
        catch (Exception ex)
        {
            // A background backup must never take the app down. The failure is stored in the backup status
            // (shown in the status bar) and logged.
            Logger?.LogError(ex, "Scheduled backup failed");
            ErrorMessage = "Zamanlanmış yedekleme yapılamadı.";
        }
    }

    /// <summary>
    /// Runs screen changes started from events (login, logout, setup finished). Event handlers are async void, so an
    /// exception would otherwise crash the process — found when the Windows test run disposed the app mid-way.
    /// </summary>
    private void RunSafely(Func<Task> work) => LastScreenChange = RunSafelyCoreAsync(work);

    private async Task RunSafelyCoreAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Screen change failed");
            ErrorMessage = "Beklenmeyen bir hata oluştu. Verileriniz korunuyor; programı yeniden başlatmanız gerekebilir.";
        }
    }

    public async Task<bool> BackupOnExitAsync()
    {
        var status = await _backups.GetStatusAsync();
        if (!status.KeyConfigured || !status.OnExit || !_session.IsAuthenticated)
        {
            return false;
        }

        var r = await _backups.CreateBackupAsync(BackupTrigger.OnExit);
        return r.IsSuccess;
    }

    private LockViewModel CreateLock()
    {
        var vm = _services.GetRequiredService<LockViewModel>();
        vm.Refresh();
        vm.SwitchUserRequested += (_, _) =>
        {
            LockScreen = null;
            RunSafely(ShowLoginAsync);
        };
        return vm;
    }
}
