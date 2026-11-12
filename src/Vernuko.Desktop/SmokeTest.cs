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
                await RenderAsync(root, 1366, 768, Path.Combine(outputDirectory, $"page-{item.Key}-1366.png"));
                await RenderAsync(root, 1920, 1080, Path.Combine(outputDirectory, $"page-{item.Key}-1920.png"));
                report.AppendLine($"page {item.Key}: ok");
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

    private static async Task RenderAsync(object dataContext, int width, int height, string file)
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
        await Task.Delay(400);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap((int)(width * dpi.DpiScaleX), (int)(height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render((Visual)window.Content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using (var fs = File.Create(file))
        {
            encoder.Save(fs);
        }

        window.Content = null;
        window.Close();
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
