using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Presentation;
using Vernuko.Security;

namespace Vernuko.Desktop;

public partial class MainWindow : Window
{
    private readonly RootViewModel _root;
    private readonly IdleLockPolicy _idle;
    private readonly DispatcherTimer _timer;
    private readonly UserSession _session;
    private int _ticks;
    private bool _closingAllowed;
    private bool _closingInProgress;

    public MainWindow(RootViewModel root)
    {
        InitializeComponent();
        _root = root;
        DataContext = root;
        _session = App.Services.GetRequiredService<UserSession>();
        _idle = new IdleLockPolicy(TimeProvider.System, TimeSpan.FromMinutes(10));
        InputManager.Current.PreProcessInput += (_, e) =>
        {
            if (e.StagingItem.Input is MouseButtonEventArgs or KeyEventArgs or MouseWheelEventArgs)
            {
                _idle.RegisterActivity();
            }
        };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += OnTick;
        _timer.Start();
        StateChanged += (_, _) => AdjustForMaximized();
        MouseLeftButtonDown += OnDragArea;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_root.Shell?.Business is { } business)
        {
            _idle.IdleTimeout = TimeSpan.FromMinutes(business.AutoLockMinutes);
        }

        if (_session.IsAuthenticated && !_session.IsLocked && _idle.ShouldLock())
        {
            App.Services.GetRequiredService<AuthService>().Lock();
        }

        // Every 5 minutes: automatic reminders (off unless the business enabled them and set a limit).
        if (++_ticks % 10 == 0)
        {
            await _root.RunMessagingAsync();
        }

        // Every 30 minutes: scheduled backup check and status refresh (only while the app is running).
        if (_ticks % 60 == 0 && _session.IsAuthenticated)
        {
            await _root.RunScheduledBackupIfDueAsync();
        }
    }

    /// <summary>Drag the window from any empty area in the top 64 px (custom title bar); double click maximises.</summary>
    private void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.GetPosition(this).Y > 64 || e.OriginalSource is not (System.Windows.Controls.Panel or System.Windows.Controls.Border or System.Windows.Controls.TextBlock))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            OnMaximize(sender, e);
            return;
        }

        DragMove();
    }

    private void AdjustForMaximized()
    {
        var t = SystemParameters.WindowResizeBorderThickness;
        RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(t.Left * 2, t.Top * 2, t.Right * 2, t.Bottom * 2) : new Thickness(0);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closingAllowed)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closingInProgress)
        {
            return; // second click on X while the exit backup is running
        }

        _closingInProgress = true;
        ClosingOverlay.Visibility = Visibility.Visible;

        // Leave the Closing event before calling Close() again: WPF throws if Close() is called while the window is
        // closing. That happened whenever the backup check finished synchronously (backups off, demo mode) — found on
        // the first Windows run. Yielding also lets the "yedek alınıyor" overlay render.
        await Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            await _root.BackupOnExitAsync();
        }
        catch (Exception ex)
        {
            // Never block closing because of a backup problem; the next start shows the failed attempt in the status bar.
            App.Services.GetRequiredService<ILogger<MainWindow>>().LogError(ex, "Exit backup failed");
        }

        _closingAllowed = true;
        _timer.Stop();
        Close();
    }
}
