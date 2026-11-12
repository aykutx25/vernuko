using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Presentation;
using Vernuko.Presentation.Pages;
using Vernuko.Remote;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Remote;

/// <summary>
/// The desktop app's own flow without WPF: the owner turns on "ana bilgisayar" in Settings, a second computer pairs
/// from the setup wizard, restarts as a connected computer and works through the same screens.
/// </summary>
public class ConnectedComputerTests
{
    /// <summary>The services App.xaml.cs composes in client mode (WPF parts replaced by the test fakes).</summary>
    private static ServiceProvider ClientContainer(TestApp host, TestEnvironment clientEnv, RemoteEndpoint endpoint)
    {
        var connection = new RemoteConnection(endpoint);
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<TimeProvider>(host.Time);
        services.AddSingleton<IAppEnvironment>(clientEnv);
        services.AddSingleton<ISecretProtector>(new TestSecretProtector());
        services.AddSingleton<IDeviceIdentity>(new TestDevice("connected-computer"));
        services.AddVernukoRemoteClient(connection);
        services.AddSingleton<IMultiComputer>(sp => new MultiComputerService(sp, connection, sp.GetRequiredService<ILogger<MultiComputerService>>()));
        services.AddSingleton<ILicenseOnlineService, HostManagedLicenseOnline>();
        services.AddSingleton<CloudFolderLocator>();
        services.AddVernukoPresentation();
        services.AddSingleton<FakeDialogs>();
        services.AddSingleton<Vernuko.Presentation.Common.IDialogService>(sp => sp.GetRequiredService<FakeDialogs>());
        services.AddSingleton<FakePlatform>();
        services.AddSingleton<Vernuko.Presentation.Common.IPlatformServices>(sp => sp.GetRequiredService<FakePlatform>());
        services.AddSingleton<FakeUpdates>();
        services.AddSingleton<Vernuko.Presentation.Common.IUpdateService>(sp => sp.GetRequiredService<FakeUpdates>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Second_computer_pairs_from_the_wizard_and_runs_the_success_scenario_on_the_main_computer()
    {
        // Main computer: Settings › Çoklu bilgisayar › "Bu bilgisayar ana bilgisayar olsun", then "Bağlantı kodu göster".
        await using var host = await TestApp.CreateAsync();
        await host.SetupBeautyAsync();
        await using var hostMode = new MultiComputerService(host.Services, host.Get<ILogger<MultiComputerService>>()) { Port = 0, Bind = IPAddress.Loopback };
        var settingsTab = new MultiComputerViewModel(hostMode, host.Get<Vernuko.Application.Common.UserSession>(), host.Get<FakeDialogs>(), host.Get<FakePlatform>(), host.Time);
        await settingsTab.EnableHostCommand.ExecuteAsync(null);
        Assert.Null(settingsTab.ErrorMessage);
        Assert.True(settingsTab.IsHost);
        Assert.NotEmpty(settingsTab.Addresses);
        settingsTab.ShowCodeCommand.Execute(null);
        Assert.NotNull(settingsTab.Code);

        // Second computer: setup wizard › "Ana bilgisayara bağlan…" (address + code), then Vernuko restarts.
        await using var second = await TestApp.CreateAsync();
        var wizardMode = new MultiComputerService(second.Services, second.Get<ILogger<MultiComputerService>>());
        var wizard = new SetupWizardViewModel(second.Get<SetupService>(), second.Get<IAuthService>(), second.Get<FakeDialogs>(), second.Get<FakePlatform>(), second.Env, second.Get<IBackupService>(), wizardMode);
        second.Get<FakeDialogs>().PromptAnswers.Enqueue($"127.0.0.1:{hostMode.Server!.Address.Port}");
        second.Get<FakeDialogs>().PromptAnswers.Enqueue(settingsTab.Code!.ToLowerInvariant());
        await wizard.ConnectToHostCommand.ExecuteAsync(null);
        Assert.Null(wizard.ErrorMessage);
        Assert.Equal(1, second.Get<FakePlatform>().Restarts);
        var endpoint = RemoteClientStore.Load(second.Env, second.Get<ISecretProtector>());
        Assert.NotNull(endpoint);

        await settingsTab.LoadAsync();
        Assert.Single(settingsTab.Computers);

        // After the restart: client mode, straight to the login screen (no setup wizard, no local database).
        await using var client = ClientContainer(host, second.Env, endpoint);
        var root = client.GetRequiredService<RootViewModel>();
        await root.StartAsync();
        var login = Assert.IsType<LoginViewModel>(root.Screen);
        login.Username = TestApp.AdminUser;
        login.Password = TestApp.AdminPassword;
        await login.LoginCommand.ExecuteAsync(null);
        Assert.Null(login.ErrorMessage);
        await root.LastScreenChange;
        var shell = Assert.IsType<ShellViewModel>(root.Screen);

        // Every screen loads its data from the main computer.
        foreach (var item in shell.NavItems.ToList())
        {
            await shell.NavigateAsync(item.Key);
            Assert.True(shell.CurrentPage is { HasError: false }, $"{item.Key}: {shell.CurrentPage?.ErrorMessage}");
        }

        var settings = Assert.IsType<SettingsViewModel>((await NavigateAsync(shell, "settings")).CurrentPage);
        Assert.True(settings.IsClient);
        Assert.False(settings.ShowMessagingTab);
        Assert.True(settings.MultiComputer.IsClient);

        // The success scenario through the connected computer's screens (backup steps belong to the main computer).
        var steps = await Vernuko.Presentation.Diagnostics.EndToEndScenario.RunAsync(shell, client, Path.Combine(second.Root, "yok"), _ => Task.CompletedTask, includeBackup: false);
        Assert.True(steps.All(s => s.Ok), string.Join("\n", steps.Select(s => $"{(s.Ok ? "ok  " : "FAIL")} {s.Name}: {s.Detail}")));
        Assert.Equal(["müşteri ekle", "paket sat", "randevu ver", "tamamla", "seans düştü"], steps.Select(s => s.Name));

        // The data is on the main computer.
        Assert.Contains((await host.Get<CustomerService>().SearchAsync("Senaryo")).Value, c => c.FullName == "Senaryo Müşterisi");

        // Removing the computer on the main computer ends its session: the screen returns to the login.
        await hostMode.RemoveComputerAsync(endpoint.DeviceId);
        await shell.NavigateAsync("customers");
        await root.LastScreenChange;
        Assert.IsType<LoginViewModel>(root.Screen);
    }

    private static async Task<ShellViewModel> NavigateAsync(ShellViewModel shell, string key)
    {
        await shell.NavigateAsync(key);
        return shell;
    }

    [Fact]
    public async Task Main_computer_mode_survives_a_restart_and_needs_the_settings_permission()
    {
        await using var host = await TestApp.CreateAsync();
        await host.SetupBeautyAsync();
        await using (var first = new MultiComputerService(host.Services, host.Get<ILogger<MultiComputerService>>()) { Port = 0, Bind = IPAddress.Loopback })
        {
            Assert.True((await first.EnableHostAsync()).IsSuccess);
        }

        await using var afterRestart = new MultiComputerService(host.Services, host.Get<ILogger<MultiComputerService>>()) { Port = 0, Bind = IPAddress.Loopback };
        Assert.Equal(ComputerRole.Standalone, afterRestart.Role);
        await afterRestart.StartIfEnabledAsync();
        Assert.Equal(ComputerRole.Host, afterRestart.Role);

        host.Get<IAuthService>().Logout();
        Assert.Equal(Vernuko.Core.Error.Forbidden.Code, afterRestart.NewConnectionCode().Error.Code);
        Assert.Equal(Vernuko.Core.Error.Forbidden.Code, (await afterRestart.DisableHostAsync()).Error.Code);
    }

    [Theory]
    [InlineData("192.168.1.20", "https://192.168.1.20:47250/")]
    [InlineData("RESEPSIYON-PC", "https://resepsiyon-pc:47250/")]
    [InlineData("10.0.0.5:5000", "https://10.0.0.5:5000/")]
    [InlineData(" https://ana.local:47250 ", "https://ana.local:47250/")]
    public void Addresses_typed_by_people_are_understood(string typed, string expected) =>
        Assert.Equal(new Uri(expected), MultiComputerService.ParseAddress(typed));
}
