using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Services;
using Vernuko.Desktop.Services;
using Vernuko.Infrastructure;
using Vernuko.Persistence;
using Vernuko.Presentation;
using Vernuko.Presentation.Common;
using Vernuko.Remote;
using Vernuko.Application.Common;
using Vernuko.Core;

namespace Vernuko.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private Mutex? _singleInstance;

    public static IServiceProvider Services => ((App)Current)._services ?? throw new InvalidOperationException("Not started.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        UseTurkishCulture();
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

        // Smoke/demo runs never contact the license server.
        services.AddSingleton(smoke || demo ? new LicensingOptions { PublicKeys = BuildSettings.LicenseKeys() } : BuildSettings.Licensing());

        // Faz 7: paired with a main computer → no local data, every call goes to the main computer.
        var endpoint = smoke || demo ? null : RemoteClientStore.Load(env, SecretProtectors.ForCurrentUser(env));
        var isClient = endpoint is not null;
        if (endpoint is not null)
        {
            var connection = new RemoteConnection(endpoint);
            services.AddVernukoPlatform(env);
            services.AddVernukoRemoteClient(connection);
            services.AddSingleton<IMultiComputer>(sp => new MultiComputerService(sp, connection, sp.GetRequiredService<ILogger<MultiComputerService>>()));
            services.AddSingleton<ILicenseOnlineService, HostManagedLicenseOnline>();
            services.AddSingleton<CloudFolderLocator>();
        }
        else
        {
            services.AddSingleton<IMultiComputer>(sp => new MultiComputerService(sp, sp.GetRequiredService<ILogger<MultiComputerService>>()));
            services.AddVernuko(env);
        }

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

        if (!isClient)
        {
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
        }

        if (demo || smoke)
        {
            var seeded = await _services.GetRequiredService<DemoDataSeeder>().SeedAsync();
            if (seeded.IsFailure)
            {
                logger.LogError("Demo seeding failed: {Code}", seeded.Error.Code);
            }

            _services.GetRequiredService<IAuthService>().Logout();
        }

        await _services.GetRequiredService<ILicenseService>().RefreshAsync();
        if (!isClient && !smoke && !demo)
        {
            await _services.GetRequiredService<IMultiComputer>().StartIfEnabledAsync();
        }

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

    /// <summary>
    /// The interface is Turkish only, so dates, numbers and sorting must be Turkish even on an English Windows:
    /// otherwise the date picker shows and parses "11/25/2026" and bound numbers use "1,234.50". WPF does not follow
    /// the system culture on its own (bindings default to en-US), hence both the thread culture and the default
    /// <see cref="FrameworkElement.Language"/>.
    /// </summary>
    private static void UseTurkishCulture()
    {
        var culture = TurkishText.Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        var language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(language));
        FrameworkContentElement.LanguageProperty.OverrideMetadata(typeof(System.Windows.Documents.TextElement), new FrameworkPropertyMetadata(language));
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
