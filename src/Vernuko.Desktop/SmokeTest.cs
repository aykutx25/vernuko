using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Presentation;
using Vernuko.Presentation.Common;
using Vernuko.Presentation.Diagnostics;
using Vernuko.Presentation.Pages;

namespace Vernuko.Desktop;

/// <summary>
/// "Vernuko.exe --smoke-test &lt;folder&gt;": runs on a throw-away demo database, opens every screen at 1366×768 and
/// 1920×1080, saves PNG screenshots and fails (exit code 2) if WPF reports any data-binding error. Used in CI on
/// Windows and by developers after UI changes. Never touches the real database.
/// </summary>
internal static class SmokeTest
{
    private static readonly List<string> UnhandledErrors = [];

    /// <summary>Screens rendered smaller than requested (the check would then not show what it claims to).</summary>
    private static readonly List<string> SizeErrors = [];

    /// <summary>The main window's MinWidth/MinHeight: the tightest layout a user can get.</summary>
    private const int MinimumWidth = 1180;

    private const int MinimumHeight = 680;

    public static void RecordUnhandled(Exception ex) => UnhandledErrors.Add(ex.GetType().Name + ": " + ex.Message);

    /// <summary>Real time shifted to next week's Wednesday 10:00 (Istanbul): inside business hours whenever the check runs.</summary>
    public static TimeProvider CreateClock()
    {
        var zone = TimeZones.Find(TimeZones.Turkey);
        var nowUtc = DateTime.UtcNow;
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var daysToWednesday = ((int)DayOfWeek.Wednesday - (int)local.DayOfWeek + 7) % 7 + 7;
        var target = DateTime.SpecifyKind(local.Date.AddDays(daysToWednesday).AddHours(10), DateTimeKind.Unspecified);
        return new ShiftedClock(TimeZoneInfo.ConvertTimeToUtc(target, zone) - nowUtc);
    }

    private sealed class ShiftedClock(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + offset;

        public override TimeZoneInfo LocalTimeZone => TimeZones.Find(TimeZones.Turkey);
    }

    /// <summary>Answers every dialog without UI: confirm = yes, prompts = their default (or a fixed reason).</summary>
    public sealed class AutoDialogService : IDialogService
    {
        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Evet", string cancelText = "Vazgeç", bool destructive = false) => Task.FromResult(true);

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;

        public Task<string?> PromptAsync(string title, string message, string? initialValue = null, bool required = true) =>
            Task.FromResult<string?>(string.IsNullOrEmpty(initialValue) ? "Otomatik kontrol" : initialValue);

        public Task<string?> PickFolderAsync(string title, string? initialFolder) => Task.FromResult(initialFolder);

        public Task<string?> PickOpenFileAsync(string title, string filter) => Task.FromResult<string?>(null);

        public Task<string?> PickSaveFileAsync(string title, string defaultFileName, string filter) => Task.FromResult<string?>(null);
    }

    public static async Task<int> RunAsync(IServiceProvider services, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var listener = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var report = new StringBuilder();

        try
        {
            // Regression check from the first Windows run: a typed password must reach the view model.
            var probe = services.GetRequiredService<LoginViewModel>();
            var box = new System.Windows.Controls.PasswordBox();
            System.Windows.Data.BindingOperations.SetBinding(box, Behaviors.PasswordBoxAssistant.BoundPasswordProperty, new System.Windows.Data.Binding(nameof(LoginViewModel.Password))
            {
                Source = probe,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });
            box.Password = "Smoke-Parola-1";
            if (probe.Password != "Smoke-Parola-1")
            {
                report.AppendLine("FAIL: PasswordBox does not update the view model");
                return Finish(outputDirectory, report, listener, 5);
            }

            probe.Password = string.Empty;
            if (box.Password.Length != 0)
            {
                report.AppendLine("FAIL: clearing the view model password does not clear the PasswordBox");
                return Finish(outputDirectory, report, listener, 5);
            }

            report.AppendLine("password binding: ok");

            // Setup wizard: render every step without completing it.
            var wizard = services.GetRequiredService<SetupWizardViewModel>();
            wizard.SelectProfileCommand.Execute(wizard.Profiles[0]);
            foreach (var step in Enum.GetValues<SetupStep>())
            {
                wizard.Step = step;
                await RenderAsync(wizard, 1366, 768, Path.Combine(outputDirectory, $"setup-{(int)step + 1:00}-{step}.png"));
            }

            var root = services.GetRequiredService<RootViewModel>();
            await root.StartAsync();
            await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, "login.png"));

            var auth = services.GetRequiredService<IAuthService>();
            var login = await auth.LoginAsync(DemoDataSeeder.DemoUsername, DemoDataSeeder.DemoPassword);
            if (login.IsFailure)
            {
                report.AppendLine("Login failed: " + login.Error);
                return Finish(outputDirectory, report, listener, 3);
            }

            await root.ShowShellAsync();
            var shell = root.Shell!;
            foreach (var item in shell.NavItems.ToList())
            {
                await shell.NavigateAsync(item, null);
                if (shell.CurrentPage is CustomersViewModel customers && customers.Results.Count > 0)
                {
                    // The customer card's tabs only have content with a customer selected.
                    customers.Selected = customers.Results[0];
                }

                var tabs = await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, $"page-{item.Key}-1366.png"), renderAllTabs: true);
                await RenderAsync(root, 1920, 1080, Path.Combine(outputDirectory, $"page-{item.Key}-1920.png"));
                await RenderAsync(root, MinimumWidth, MinimumHeight, Path.Combine(outputDirectory, $"page-{item.Key}-min.png"));
                report.AppendLine($"page {item.Key}: ok ({tabs} tabs rendered)");
            }

            await shell.NavigateAsync("appointments", new OpenNewAppointment(null));
            await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, "appointments-editor.png"));
            if (shell.CurrentPage is AppointmentsViewModel appointments)
            {
                appointments.View = CalendarView.Week;
                await appointments.RefreshAsync();
                await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, "appointments-week.png"));
                appointments.View = CalendarView.Month;
                await appointments.RefreshAsync();
                await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, "appointments-month.png"));
                appointments.View = CalendarView.Day;
                var row = appointments.Rows.OfType<AppointmentRow>().FirstOrDefault();
                if (row is not null)
                {
                    appointments.TakePaymentCommand.Execute(row);
                    await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, "payment-dialog.png"));
                    shell.CloseOverlay();
                }
            }

            // The product's success flow, end to end, in the real WPF app (screenshot after each step).
            var steps = await EndToEndScenario.RunAsync(shell, services, Path.Combine(outputDirectory, "senaryo-yedek"),
                name => RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, $"senaryo-{name}.png")));
            foreach (var step in steps)
            {
                report.AppendLine($"senaryo {(step.Ok ? "ok  " : "HATA")} {step.Name}: {step.Detail}");
            }

            if (steps.Count == 0 || steps.Any(s => !s.Ok))
            {
                return Finish(outputDirectory, report, listener, 7);
            }

            // Regression check from the first Windows run: closing the main window threw when the exit-backup check
            // finished synchronously (backups off). The demo database has backups off, so this exercises that path.
            var main = new MainWindow(root) { Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
            var closed = new TaskCompletionSource();
            main.Closed += (_, _) => closed.TrySetResult();
            main.Show();
            await Task.Delay(300);
            main.Close();
            if (await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(60))) != closed.Task)
            {
                report.AppendLine("FAIL: main window did not close");
                return Finish(outputDirectory, report, listener, 6);
            }

            report.AppendLine("close main window: ok");

            services.GetRequiredService<UserSession>().Lock();
            await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, "lock.png"));
        }
        catch (Exception ex)
        {
            report.AppendLine("EXCEPTION: " + ex);
            return Finish(outputDirectory, report, listener, 4);
        }

        return Finish(outputDirectory, report, listener, 0);
    }

    private static int Finish(string dir, StringBuilder report, BindingErrorListener listener, int code)
    {
        report.AppendLine($"unhandled errors: {UnhandledErrors.Count}");
        foreach (var e in UnhandledErrors)
        {
            report.AppendLine("  " + e);
        }

        if (code == 0 && UnhandledErrors.Count > 0)
        {
            code = 4;
        }

        report.AppendLine($"size errors: {SizeErrors.Count}");
        foreach (var e in SizeErrors)
        {
            report.AppendLine("  " + e);
        }

        if (code == 0 && SizeErrors.Count > 0)
        {
            code = 8;
        }

        report.AppendLine($"binding errors: {listener.Errors.Count}");
        foreach (var e in listener.Errors.Distinct())
        {
            report.AppendLine("  " + e);
        }

        // UTF-8 *with* BOM: Windows PowerShell 5.1 (Get-Content) otherwise reads it as ANSI and garbles Turkish letters.
        File.WriteAllText(Path.Combine(dir, "smoke-report.txt"), report.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return code != 0 ? code : listener.Errors.Count > 0 ? 2 : 0;
    }

    /// <summary>Renders the content to a PNG. With <paramref name="renderAllTabs"/> every visible tab of the page's first
    /// TabControl is selected and rendered too, so bindings inside non-default tabs are exercised as well.</summary>
    /// <remarks>
    /// Windows never makes a window larger than the screen (the CI runner's is 1024×768), so a 1366×768 window came
    /// out about 1030 px wide and every "1366"/"1920" shot showed the same cramped layout. The page is therefore laid
    /// out at exactly the requested size on a <see cref="System.Windows.Controls.Canvas"/> (which never squeezes its
    /// children) inside the hidden window, and that element is rendered.
    /// </remarks>
    private static async Task<int> RenderAsync(object dataContext, int width, int height, string file, bool renderAllTabs = false)
    {
        object? content = dataContext is RootViewModel r ? r.Screen : dataContext;
        if (dataContext is RootViewModel { LockScreen: { } lockVm })
        {
            content = lockVm;
        }

        var page = new System.Windows.Controls.Border
        {
            Width = width,
            Height = height,
            Background = (Brush)System.Windows.Application.Current.FindResource("AppBackgroundBrush"),
            Child = new System.Windows.Controls.ContentControl { Content = content },
        };
        var canvas = new System.Windows.Controls.Canvas();
        canvas.Children.Add(page);
        var window = new Window
        {
            Width = width,
            Height = height,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            FontFamily = (FontFamily)System.Windows.Application.Current.FindResource("AppFont"),
            FontSize = 14,
            Content = canvas,
        };

        window.Show();
        await SnapshotAsync(page, width, height, file);
        var rendered = 0;
        if (renderAllTabs && FindTabControls(page).FirstOrDefault() is { } tabControl)
        {
            for (var i = 0; i < tabControl.Items.Count; i++)
            {
                if (tabControl.Items[i] is System.Windows.Controls.TabItem { Visibility: Visibility.Visible })
                {
                    tabControl.SelectedIndex = i;
                    await SnapshotAsync(page, width, height, Path.ChangeExtension(file, null) + $"-tab{i + 1:00}.png");
                    rendered++;
                }
            }
        }

        page.Child = null;
        window.Content = null;
        window.Close();
        return rendered;
    }

    private static async Task SnapshotAsync(FrameworkElement page, int width, int height, string file)
    {
        await Task.Delay(400);
        await page.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        page.UpdateLayout();
        if (Math.Abs(page.ActualWidth - width) > 0.5 || Math.Abs(page.ActualHeight - height) > 0.5)
        {
            SizeErrors.Add($"{Path.GetFileName(file)}: laid out at {page.ActualWidth:0}×{page.ActualHeight:0} instead of {width}×{height}");
        }

        var dpi = VisualTreeHelper.GetDpi(page);
        var bitmap = new RenderTargetBitmap((int)(width * dpi.DpiScaleX), (int)(height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(page);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var fs = File.Create(file);
        encoder.Save(fs);
    }

    private static IEnumerable<System.Windows.Controls.TabControl> FindTabControls(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.TabControl tc)
            {
                yield return tc;
            }

            foreach (var nested in FindTabControls(child))
            {
                yield return nested;
            }
        }
    }

    private sealed class BindingErrorListener : TraceListener
    {
        private readonly StringBuilder _line = new();

        public List<string> Errors { get; } = [];

        public override void Write(string? message) => _line.Append(message);

        public override void WriteLine(string? message)
        {
            _line.Append(message);
            var text = _line.ToString();
            _line.Clear();
            if (text.Contains("Error", StringComparison.Ordinal))
            {
                Errors.Add(text);
            }
        }
    }
}
