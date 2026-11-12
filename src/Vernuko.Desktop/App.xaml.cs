using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Services;
using Vernuko.Desktop.Services;
using Vernuko.Infrastructure;
using Vernuko.Persistence;
using Vernuko.Presentation;
using Vernuko.Presentation.Common;

namespace Vernuko.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private Mutex? _singleInstance;

    public static IServiceProvider Services => ((App)Current)._services ?? throw new InvalidOperationException("Not started.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var demo = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var smokeIndex = Array.FindIndex(e.Args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));
        var smoke = smokeIndex >= 0;

        if (!smoke)
        {
            _singleInstance = new Mutex(true, demo ? "Vernuko.Desktop.Demo" : "Vernuko.Desktop", out var created);
            if (!created)
            {
                MessageBox.Show("Vernuko zaten açık.", "Vernuko", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }
        }

        var env = smoke
            ? new AppEnvironment(Path.Combine(Path.GetTempPath(), "vernuko-smoke", Guid.NewGuid().ToString("N")), isDemo: true)
            : AppEnvironment.CreateDefault(demo);
        env.CleanTemp();

        var services = new ServiceCollection();
        if (smoke)
        {
            // Deterministic check run: business-day morning clock and auto-answered dialogs (nobody is at the keyboard).
            services.AddSingleton<TimeProvider>(SmokeTest.CreateClock());
            services.AddSingleton<IDialogService, SmokeTest.AutoDialogService>();
        }
        else
        {
            services.AddSingleton<IDialogService, WpfDialogService>();
        }

        services.AddVernuko(env);
        services.AddVernukoPresentation();
        services.AddSingleton<IPlatformServices, WpfPlatformServices>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        _services = services.BuildServiceProvider();
        var logger = _services.GetRequiredService<ILogger<App>>();
        ViewModelBase.UseLogger(logger);
        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unhandled UI exception");
            if (smoke)
            {
                // The smoke test must report, not block on a dialog nobody can click.
                SmokeTest.RecordUnhandled(args.Exception);
                args.Handled = true;
                return;
            }

            MessageBox.Show("Beklenmeyen bir hata oluştu. Verileriniz kaydedildiği son halde korunuyor; ayrıntılar günlük dosyasına yazıldı.", "Vernuko", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        try
        {
            await _services.GetRequiredService<DatabaseInitializer>().InitializeAsync(env.DatabasePath, env.SafetyDirectory, CancellationToken.None);
        }
        catch (Exception ex) when (ex is DatabaseTooNewException or DatabaseMigrationFailedException)
        {
            logger.LogError(ex, "Database initialisation failed");
            MessageBox.Show(ex.Message, "Vernuko", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (demo || smoke)
        {
            var seeded = await _services.GetRequiredService<DemoDataSeeder>().SeedAsync();
            if (seeded.IsFailure)
            {
                logger.LogError("Demo seeding failed: {Code}", seeded.Error.Code);
            }

            _services.GetRequiredService<AuthService>().Logout();
        }

        await _services.GetRequiredService<LicenseService>().RefreshAsync();

        if (smoke)
        {
            var output = smokeIndex + 1 < e.Args.Length ? e.Args[smokeIndex + 1] : Path.Combine(Environment.CurrentDirectory, "smoke-output");
            var code = await SmokeTest.RunAsync(_services, output);
            Shutdown(code);
            return;
        }

        var root = _services.GetRequiredService<RootViewModel>();
        var window = new MainWindow(root);
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        await root.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Shutdown must never end with an error dialog: data is already committed (WAL, synchronous=FULL).
        try
        {
            _services?.Dispose();
            SqliteSetup.ReleaseFileHandles();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ObjectDisposedException)
        {
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
