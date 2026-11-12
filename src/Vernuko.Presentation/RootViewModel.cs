using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
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

    public async Task StartAsync()
    {
        if (!await _setup.IsCompletedAsync())
        {
            var wizard = _services.GetRequiredService<SetupWizardViewModel>();
            wizard.Completed += async (_, _) => await ShowShellAsync();
            Screen = wizard;
            return;
        }

        await ShowLoginAsync();
    }

    public async Task ShowLoginAsync()
    {
        var login = _services.GetRequiredService<LoginViewModel>();
        login.LoggedIn += async (_, _) => await ShowShellAsync();
        await login.LoadAsync();
        Screen = login;
    }

    public async Task ShowShellAsync()
    {
        var shell = _services.GetRequiredService<ShellViewModel>();
        shell.LoggedOut += async (_, _) => await ShowLoginAsync();
        Screen = shell;
        await shell.InitializeAsync();
        await RunScheduledBackupIfDueAsync();
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = "Zamanlanmış yedekleme yapılamadı.";
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
        vm.SwitchUserRequested += async (_, _) =>
        {
            LockScreen = null;
            await ShowLoginAsync();
        };
        return vm;
    }
}
