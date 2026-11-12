using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Remote;
using Vernuko.Security;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Remote;

/// <summary>A main computer (real stack + embedded server with TLS) and connected computers talking to it.</summary>
public sealed class RemoteRig : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _clients = [];

    private RemoteRig(TestApp host, RemoteHostServer server, Uri address)
    {
        Host = host;
        Server = server;
        Address = address;
    }

    public TestApp Host { get; }

    public RemoteHostServer Server { get; }

    public Uri Address { get; }

    public static async Task<RemoteRig> StartAsync(bool tls = true)
    {
        var host = await TestApp.CreateAsync();
        await host.SetupBeautyAsync();
        var cert = tls ? RemoteHostCertificate.LoadOrCreate(host.Env, host.Get<ISecretProtector>()) : null;
        var server = await RemoteHostServer.StartAsync(host.Services, cert, port: 0, bind: IPAddress.Loopback, version: "test");
        var address = new Uri($"{(tls ? "https" : "http")}://127.0.0.1:{server.Address.Port}/");
        return new RemoteRig(host, server, address);
    }

    public async Task<RemoteEndpoint> PairAsync(string name = "Resepsiyon")
    {
        var code = Server.NewPairingCode().Code;
        var paired = await RemoteConnection.PairAsync(Address, code.ToLowerInvariant(), name);
        Assert.True(paired.IsSuccess, paired.IsFailure ? paired.Error.ToString() : null);
        return paired.Value;
    }

    /// <summary>A connected computer's service container (what the desktop app builds in client mode).</summary>
    public ServiceProvider Client(RemoteEndpoint endpoint)
    {
        var services = new ServiceCollection();
        services.AddVernukoRemoteClient(new RemoteConnection(endpoint));
        var sp = services.BuildServiceProvider();
        _clients.Add(sp);
        return sp;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _clients)
        {
            await c.DisposeAsync();
        }

        await Server.DisposeAsync();
        await Host.DisposeAsync();
    }
}

public class RemoteJsonTests
{
    [Fact]
    public void Results_and_errors_round_trip()
    {
        var ok = Result.Success<IReadOnlyList<Guid>>([Guid.NewGuid()]);
        var back = System.Text.Json.JsonSerializer.Deserialize<Result<IReadOnlyList<Guid>>>(System.Text.Json.JsonSerializer.Serialize(ok, RpcJson.Options), RpcJson.Options)!;
        Assert.True(back.IsSuccess);
        Assert.Equal(ok.Value, back.Value);

        Result fail = new Error("x.y", "Türkçe ğüşıöç mesaj");
        var failBack = System.Text.Json.JsonSerializer.Deserialize<Result>(System.Text.Json.JsonSerializer.Serialize(fail, RpcJson.Options), RpcJson.Options)!;
        Assert.True(failBack.IsFailure);
        Assert.Equal(fail.Error, failBack.Error);

        var schedule = WeeklySchedule.DefaultBusinessWeek();
        var s2 = System.Text.Json.JsonSerializer.Deserialize<WeeklySchedule>(System.Text.Json.JsonSerializer.Serialize(schedule, RpcJson.Options), RpcJson.Options)!;
        Assert.Equal(schedule.For(DayOfWeek.Monday), s2.For(DayOfWeek.Monday));
    }

    [Fact]
    public void Every_contract_method_is_remotable_and_host_only_names_exist()
    {
        Assert.NotEmpty(RpcContracts.Methods);
        foreach (var key in RpcContracts.HostOnly)
        {
            Assert.True(RpcContracts.Methods.ContainsKey(key), key);
        }
    }
}

public class RemoteTests
{
    [Fact]
    public async Task Connected_computer_pairs_signs_in_and_works_on_the_main_computers_data()
    {
        await using var rig = await RemoteRig.StartAsync();
        var client = rig.Client(await rig.PairAsync());

        var auth = client.GetRequiredService<IAuthService>();
        Assert.True((await auth.LoginAsync(TestApp.AdminUser, "yanlis")).IsFailure);
        Assert.True((await auth.LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        Assert.True(client.GetRequiredService<UserSession>().Has(Permission.CustomersManage));

        // Customer created on the connected computer is in the main computer's database.
        var customers = client.GetRequiredService<ICustomerService>();
        var created = await customers.CreateAsync(new CustomerDetails("Uzak", "Müşteri", "0532 999 88 77", CompanyName: null), allowDuplicates: true);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.ToString() : null);
        Assert.Contains((await rig.Host.Get<CustomerService>().SearchAsync("uzak")).Value, c => c.Id == created.Value);

        // Booking from the connected computer and from the main computer for the same slot: only one wins.
        var c = await rig.Host.CatalogAsync();
        var request = new CreateAppointmentRequest(created.Value, c.Laser.Id, c.Selin.Id, [c.Room1.Id, c.LaserDevice.Id], TestApp.Local(14), null, null, null, null);
        var remoteBooking = await client.GetRequiredService<IAppointmentService>().CreateAsync(request, confirmWarnings: true);
        Assert.True(remoteBooking.IsSuccess && remoteBooking.Value.Saved, remoteBooking.IsFailure ? remoteBooking.Error.ToString() : string.Join("; ", remoteBooking.Value.Issues.Select(i => i.Message)));
        var hostCustomer = await rig.Host.AddCustomerAsync("Yerel", "Müşteri", "0532 999 88 66");
        var hostBooking = await rig.Host.Get<AppointmentService>().CreateAsync(request with { CustomerId = hostCustomer }, confirmWarnings: true);
        Assert.False(hostBooking.Value.Saved);
        Assert.Contains(hostBooking.Value.Issues, i => i.Code.StartsWith("conflict.", StringComparison.Ordinal));

        // Lists, slots (records with nested lists), settings and license status arrive intact.
        var day = DateOnly.FromDateTime(TestApp.Local(14));
        var agenda = await client.GetRequiredService<IAppointmentService>().GetDayAsync(day, new AppointmentFilter());
        Assert.True(agenda.IsSuccess, agenda.IsFailure ? agenda.Error.ToString() : null);
        Assert.Contains(agenda.Value.Items, a => a.CustomerId == created.Value);
        Assert.NotEmpty(agenda.Value.Breaks);
        var info = await client.GetRequiredService<ISettingsService>().GetBusinessInfoAsync();
        Assert.Equal("Işıl Güzellik ve Lazer", info!.BusinessName);
        var license = client.GetRequiredService<ILicenseService>();
        Assert.Equal(rig.Host.Get<LicenseService>().Current.State, (await license.RefreshAsync()).State);
        Assert.True(license.CanWrite);

        // Background/host-only work and host disk paths are refused.
        var restore = await client.GetRequiredService<IBackupService>().RestoreAsync("C:\\gizli.vbk", null);
        Assert.True(restore.IsFailure);
        Assert.Equal("remote.host_only", restore.Error.Code);
    }

    [Fact]
    public async Task Permissions_are_checked_on_the_main_computer_per_session()
    {
        await using var rig = await RemoteRig.StartAsync();
        Assert.True((await rig.Host.Get<UserAdminService>().CreateAsync("sekreter", "Sekreter", "Randevu-Masasi-1", SystemRole.Secretary)).IsSuccess);
        var endpoint = await rig.PairAsync();
        var secretary = rig.Client(endpoint);
        var owner = rig.Client(endpoint);
        Assert.True((await secretary.GetRequiredService<IAuthService>().LoginAsync("sekreter", "Randevu-Masasi-1")).IsSuccess);
        Assert.True((await owner.GetRequiredService<IAuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);

        // Same computer, two people: the secretary's session cannot manage users, the owner's can.
        Assert.False(secretary.GetRequiredService<UserSession>().Has(Permission.UsersManage));
        var denied = await secretary.GetRequiredService<IUserAdminService>().ListAsync();
        Assert.True(denied.IsFailure);
        Assert.Equal(Error.Forbidden.Code, denied.Error.Code);
        Assert.True((await owner.GetRequiredService<IUserAdminService>().ListAsync()).IsSuccess);

        // Locking one computer locks only that session on the main computer.
        secretary.GetRequiredService<IAuthService>().Lock();
        await Task.Delay(200);
        Assert.True((await owner.GetRequiredService<ICustomerService>().SearchAsync(null)).IsSuccess);
        Assert.True((await secretary.GetRequiredService<IAuthService>().UnlockAsync("Randevu-Masasi-1")).IsSuccess);
        Assert.True((await secretary.GetRequiredService<ICustomerService>().SearchAsync(null)).IsSuccess);
    }

    [Fact]
    public async Task Wrong_code_another_certificate_and_removed_devices_are_refused()
    {
        await using var rig = await RemoteRig.StartAsync();
        rig.Server.NewPairingCode();
        var wrong = await RemoteConnection.PairAsync(rig.Address, "AAAA-BBBB-CCCC", "Saldırgan");
        Assert.True(wrong.IsFailure);
        Assert.Equal("remote.bad_code", wrong.Error.Code);

        var endpoint = await rig.PairAsync();

        // A different certificate at the same address (e.g. another device pretending to be the main computer).
        var spoofed = rig.Client(endpoint with { Fingerprint = new string('0', 64) });
        var r = await spoofed.GetRequiredService<IAuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
        Assert.True(r.IsFailure);
        Assert.Equal("remote.certificate", r.Error.Code);

        // A wrong device key is not accepted; removing the device ends its session at once.
        var forged = rig.Client(endpoint with { DeviceKey = Convert.ToBase64String(new byte[32]) });
        Assert.Equal("remote.device", (await forged.GetRequiredService<IAuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).Error.Code);
        var client = rig.Client(endpoint);
        Assert.True((await client.GetRequiredService<IAuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        await rig.Server.RemoveDeviceAsync(endpoint.DeviceId);
        var after = await client.GetRequiredService<ICustomerService>().SearchAsync(null);
        Assert.True(after.IsFailure);
        Assert.Equal("remote.device", after.Error.Code);
    }

    [Fact]
    public async Task Trial_allows_two_connected_computers_and_a_code_works_once()
    {
        await using var rig = await RemoteRig.StartAsync();
        await rig.PairAsync("PC 1");
        var code = rig.Server.NewPairingCode().Code;
        Assert.True((await RemoteConnection.PairAsync(rig.Address, code, "PC 2")).IsSuccess);
        var reused = await RemoteConnection.PairAsync(rig.Address, code, "PC 2 tekrar");
        Assert.Equal("remote.no_code", reused.Error.Code);
        var third = await RemoteConnection.PairAsync(rig.Address, rig.Server.NewPairingCode().Code, "PC 3");
        Assert.Equal("remote.device_limit", third.Error.Code);
        Assert.Equal(2, (await rig.Server.Devices.ListAsync()).Count);
    }

    [Fact]
    public async Task Main_computer_offline_gives_a_clear_message()
    {
        var endpoint = new RemoteEndpoint(new Uri("https://127.0.0.1:9/"), new string('a', 64), "pc_x", "key");
        var services = new ServiceCollection();
        services.AddVernukoRemoteClient(new RemoteConnection(endpoint));
        await using var client = services.BuildServiceProvider();
        var r = await client.GetRequiredService<IAuthService>().LoginAsync("a", "b");
        Assert.Equal("remote.unavailable", r.Error.Code);
        var exception = await Assert.ThrowsAsync<RemoteCallException>(() => client.GetRequiredService<IBackupService>().GetStatusAsync());
        Assert.Equal("remote.unavailable", exception.Error.Code);
    }
}
