using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Remote;

/// <summary>The pairing data a connected computer keeps (encrypted with the Windows account key).</summary>
public static class RemoteClientStore
{
    private const string FileName = "remote-client.protected";

    public static RemoteEndpoint? Load(IAppEnvironment env, ISecretProtector protector)
    {
        var path = Path.Combine(env.DataDirectory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RemoteEndpoint>(protector.Unprotect(File.ReadAllBytes(path)), RpcJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or System.Security.Cryptography.CryptographicException or IOException)
        {
            return null;
        }
    }

    public static void Save(IAppEnvironment env, ISecretProtector protector, RemoteEndpoint endpoint) =>
        File.WriteAllBytes(Path.Combine(env.DataDirectory, FileName), protector.Protect(JsonSerializer.SerializeToUtf8Bytes(endpoint, RpcJson.Options)));

    public static void Delete(IAppEnvironment env) => File.Delete(Path.Combine(env.DataDirectory, FileName));
}

/// <summary>
/// Main computer: starts/stops the embedded server and manages connected computers. Connected computer: knows
/// its main computer. Changing the role takes effect after a restart (the services are composed at start-up).
/// </summary>
public sealed class MultiComputerService : IMultiComputer, IAsyncDisposable, IDisposable
{
    public const string HostEnabledKey = "remote.host_enabled";

    private static readonly Error NeedsPermission = Error.Forbidden;

    private readonly IServiceProvider _services;
    private readonly RemoteConnection? _client;
    private readonly ILogger<MultiComputerService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RemoteHostServer? _server;

    /// <summary>Main computer or stand-alone computer.</summary>
    public MultiComputerService(IServiceProvider services, ILogger<MultiComputerService> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>Connected computer.</summary>
    public MultiComputerService(IServiceProvider services, RemoteConnection client, ILogger<MultiComputerService> logger)
        : this(services, logger)
    {
        _client = client;
        client.SessionEnded += (s, e) => SessionEnded?.Invoke(this, e);
    }

    public event EventHandler? SessionEnded;

    public ComputerRole Role => _client is not null ? ComputerRole.Client : _server is not null ? ComputerRole.Host : ComputerRole.Standalone;

    /// <summary>Port the main computer listens on (tests use a free port).</summary>
    public int Port { get; init; } = RemoteHostServer.DefaultPort;

    /// <summary>Tests bind to the loopback address; the app listens on the local network.</summary>
    public IPAddress? Bind { get; init; }

    public RemoteHostServer? Server => _server;

    public IReadOnlyList<string> Addresses => _client is not null
        ? [_client.Endpoint.Address.Authority]
        : _server is null ? [] : LocalAddresses().Select(a => $"{a}:{_server.Address.Port}").ToList();

    public async Task<Result> EnableHostAsync(CancellationToken ct = default)
    {
        if (!CanManage())
        {
            return NeedsPermission;
        }

        if (_client is not null)
        {
            return new Error("remote.is_client", "Bu bilgisayar başka bir ana bilgisayara bağlı.");
        }

        var started = await StartAsync();
        if (started.IsSuccess)
        {
            await SetEnabledAsync(true, ct);
        }

        return started;
    }

    public async Task<Result> DisableHostAsync(CancellationToken ct = default)
    {
        if (!CanManage())
        {
            return NeedsPermission;
        }

        await SetEnabledAsync(false, ct);
        await StopAsync();
        return Result.Success();
    }

    public async Task StartIfEnabledAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            return;
        }

        await using var db = await _services.GetRequiredService<IDbContextFactory<VernukoDbContext>>().CreateDbContextAsync(ct);
        if (await SettingsStore.GetAsync(db, HostEnabledKey, ct) == "1")
        {
            var r = await StartAsync();
            if (r.IsFailure)
            {
                _logger.LogWarning("Main-computer server could not start: {Code}", r.Error.Code);
            }
        }
    }

    public Result<ConnectionCode> NewConnectionCode()
    {
        if (!CanManage())
        {
            return NeedsPermission;
        }

        if (_server is null)
        {
            return new Error("remote.not_host", "Önce \"Bu bilgisayar ana bilgisayar olsun\" seçeneğini açın.");
        }

        var code = _server.NewPairingCode();
        return new ConnectionCode(code.Code, code.ExpiresAtUtc);
    }

    public async Task<IReadOnlyList<PairedComputer>> ListComputersAsync(CancellationToken ct = default)
    {
        var registry = _server?.Devices ?? new RemoteDeviceRegistry(_services.GetRequiredService<IDbContextFactory<VernukoDbContext>>(), _services.GetRequiredService<TimeProvider>());
        return _client is not null ? [] : (await registry.ListAsync(ct)).Select(d => new PairedComputer(d.Id, d.Name, d.PairedAtUtc, d.LastSeenUtc)).ToList();
    }

    public async Task<Result> RemoveComputerAsync(string id, CancellationToken ct = default)
    {
        if (!CanManage())
        {
            return NeedsPermission;
        }

        if (_server is not null)
        {
            await _server.RemoveDeviceAsync(id);
        }
        else
        {
            await new RemoteDeviceRegistry(_services.GetRequiredService<IDbContextFactory<VernukoDbContext>>(), _services.GetRequiredService<TimeProvider>()).RemoveAsync(id, ct);
        }

        return Result.Success();
    }

    public async Task<Result> PairWithHostAsync(string address, string code, string computerName, CancellationToken ct = default)
    {
        if (_client is not null || _server is not null)
        {
            return new Error("remote.role", "Bu bilgisayar zaten çoklu bilgisayar modunda.");
        }

        if (ParseAddress(address) is not { } uri)
        {
            return new Error("remote.address", "Ana bilgisayarın adresini girin (ör. 192.168.1.20 veya RESEPSIYON-PC).");
        }

        var paired = await RemoteConnection.PairAsync(uri, code, string.IsNullOrWhiteSpace(computerName) ? Environment.MachineName : computerName, ct);
        if (paired.IsFailure)
        {
            return paired.Error;
        }

        RemoteClientStore.Save(_services.GetRequiredService<IAppEnvironment>(), _services.GetRequiredService<ISecretProtector>(), paired.Value);
        return Result.Success();
    }

    public Task<Result> ForgetHostAsync(CancellationToken ct = default)
    {
        RemoteClientStore.Delete(_services.GetRequiredService<IAppEnvironment>());
        return Task.FromResult(Result.Success());
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _gate.Dispose();
    }

    /// <summary>The container is disposed synchronously when the app exits.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>"192.168.1.20", "RESEPSIYON-PC", "host:47250" or a full https:// address.</summary>
    public static Uri? ParseAddress(string address)
    {
        var text = address.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return null;
        }

        var port = uri.IsDefaultPort && !address.Contains(":443", StringComparison.Ordinal) ? RemoteHostServer.DefaultPort : uri.Port;
        return new UriBuilder(uri.Scheme, uri.Host, port, "/").Uri;
    }

    private bool CanManage() => _services.GetRequiredService<UserSession>().Has(Permission.SettingsManage);

    private async Task<Result> StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_server is not null)
            {
                return Result.Success();
            }

            var cert = RemoteHostCertificate.LoadOrCreate(_services.GetRequiredService<IAppEnvironment>(), _services.GetRequiredService<ISecretProtector>());
            var version = _services.GetRequiredService<IAppEnvironment>().AppVersion;
            _server = await RemoteHostServer.StartAsync(_services, cert, Port, Bind, version);
            _logger.LogInformation("Main-computer server listening on port {Port}", _server.Address.Port);
            return Result.Success();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Main-computer server could not start");
            return new Error("remote.port", $"Ağ bağlantı noktası {Port} kullanımda. Başka bir program bu portu kullanıyor olabilir.");
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning(ex, "Main-computer server could not start");
            return new Error("remote.start", "Ana bilgisayar sunucusu başlatılamadı. Ayrıntı günlük dosyasında.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_server is not null)
            {
                await _server.DisposeAsync();
                _server = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SetEnabledAsync(bool enabled, CancellationToken ct)
    {
        var time = _services.GetRequiredService<TimeProvider>();
        await using var db = await _services.GetRequiredService<IDbContextFactory<VernukoDbContext>>().CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SettingsStore.SetAsync(db, HostEnabledKey, enabled ? "1" : "0", time.GetUtcNow().UtcDateTime, ct);
        await _services.GetRequiredService<AuditWriter>().AppendAsync(db, enabled ? "remote.host_enabled" : "remote.host_disabled", "Remote", null,
            enabled ? "Ana bilgisayar modu açıldı" : "Ana bilgisayar modu kapatıldı", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static IEnumerable<string> LocalAddresses()
    {
        var names = new List<string> { Dns.GetHostName() };
        try
        {
            names.AddRange(NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                .Select(a => a.Address.ToString()));
        }
        catch (NetworkInformationException)
        {
        }

        return names.Distinct();
    }
}
