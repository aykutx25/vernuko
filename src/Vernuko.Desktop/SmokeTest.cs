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
using Vernuko.Presentation;
using Vernuko.Presentation.Pages;

namespace Vernuko.Desktop;

/// <summary>
/// "Vernuko.exe --smoke-test &lt;folder&gt;": runs on a throw-away demo database, opens every screen at 1366×768 and
/// 1920×1080, saves PNG screenshots and fails (exit code 2) if WPF reports any data-binding error. Used in CI on
/// Windows and by developers after UI changes. Never touches the real database.
/// </summary>
internal static class SmokeTest
{
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

            var auth = services.GetRequiredService<AuthService>();
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
                var tabs = await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, $"page-{item.Key}-1366.png"), renderAllTabs: true);
                await RenderAsync(root, 1920, 1080, Path.Combine(outputDirectory, $"page-{item.Key}-1920.png"));
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
        report.AppendLine($"binding errors: {listener.Errors.Count}");
        foreach (var e in listener.Errors.Distinct())
        {
            report.AppendLine("  " + e);
        }

        File.WriteAllText(Path.Combine(dir, "smoke-report.txt"), report.ToString());
        return code != 0 ? code : listener.Errors.Count > 0 ? 2 : 0;
    }

    /// <summary>Renders the content to a PNG. With <paramref name="renderAllTabs"/> every visible tab of the page's first
    /// TabControl is selected and rendered too, so bindings inside non-default tabs are exercised as well.</summary>
    private static async Task<int> RenderAsync(object dataContext, int width, int height, string file, bool renderAllTabs = false)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Background = (Brush)System.Windows.Application.Current.FindResource("AppBackgroundBrush"),
            FontFamily = (FontFamily)System.Windows.Application.Current.FindResource("AppFont"),
            FontSize = 14,
            Content = new System.Windows.Controls.ContentControl { Content = dataContext is RootViewModel r ? r.Screen : dataContext },
        };
        if (dataContext is RootViewModel { LockScreen: { } lockVm })
        {
            window.Content = new System.Windows.Controls.ContentControl { Content = lockVm };
        }

        window.Show();
        await SnapshotAsync(window, width, height, file);
        var rendered = 0;
        if (renderAllTabs && FindTabControls(window).FirstOrDefault() is { } tabControl)
        {
            for (var i = 0; i < tabControl.Items.Count; i++)
            {
                if (tabControl.Items[i] is System.Windows.Controls.TabItem { Visibility: Visibility.Visible })
                {
                    tabControl.SelectedIndex = i;
                    await SnapshotAsync(window, width, height, Path.ChangeExtension(file, null) + $"-tab{i + 1:00}.png");
                    rendered++;
                }
            }
        }

        window.Content = null;
        window.Close();
        return rendered;
    }

    private static async Task SnapshotAsync(Window window, int width, int height, string file)
    {
        await Task.Delay(400);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap((int)(width * dpi.DpiScaleX), (int)(height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render((Visual)window.Content);
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
