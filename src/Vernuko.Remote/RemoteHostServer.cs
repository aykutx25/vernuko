using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Services;
using Vernuko.Core;

namespace Vernuko.Remote;

public sealed record PairRequest(string DeviceName, string Nonce, string Proof);

public sealed record PairResponse(string DeviceId, string DeviceKey, string HostProof);

public sealed record LoginRequest(string Username, string Password);

public sealed record HelloResponse(string Product, string Version);

public sealed record PairingCode(string Code, DateTime ExpiresAtUtc);

/// <summary>
/// The main computer's endpoint for connected computers: pairing, sign-in and calls. Runs inside the Vernuko
/// desktop app (embedded Kestrel) only while "ana bilgisayar" mode is on. Every call except hello/pair needs a
/// paired device key; every service call also needs a signed-in session.
/// </summary>
public sealed class RemoteHostServer : IAsyncDisposable
{
    public const int DefaultPort = 47250;
    public const string DeviceIdHeader = "X-Vernuko-Device";
    public const string DeviceKeyHeader = "X-Vernuko-Device-Key";

    /// <summary>Both computers must run the same Vernuko: calls are matched by position and new fields would be dropped.</summary>
    public const string VersionHeader = "X-Vernuko-Version";

    public static readonly string ProtocolVersion =
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(RemoteHostServer).Assembly)?.InformationalVersion ?? "0")
        .Split('+')[0];

    private readonly WebApplication _app;
    private readonly object _codeLock = new();

    /// <summary>Check code → check device limit → add device → use up the code, as one step (two requests at once got two devices).</summary>
    private readonly SemaphoreSlim _pairGate = new(1, 1);
    private PairingCode? _code;
    private int _failedPairings;

    private RemoteHostServer(WebApplication app, string fingerprint, RemoteSessionHost sessions, RemoteDeviceRegistry devices, TimeProvider time)
    {
        _app = app;
        Fingerprint = fingerprint;
        Sessions = sessions;
        Devices = devices;
        Time = time;
    }

    public string Fingerprint { get; }

    public RemoteSessionHost Sessions { get; }

    public RemoteDeviceRegistry Devices { get; }

    private TimeProvider Time { get; }

    public Uri Address { get; private set; } = new("https://localhost");

    /// <summary>How many connected computers the license allows (trial: 2; licensed: devices in the license − 1).</summary>
    public static async Task<int> AllowedDevicesAsync(ILicenseService license)
    {
        var doc = await license.GetDocumentAsync();
        return doc is null ? 2 : Math.Max(0, doc.MaxDevices - 1);
    }

    /// <param name="certificate">TLS certificate; null only for tests on the loopback address.</param>
    public static async Task<RemoteHostServer> StartAsync(IServiceProvider host, X509Certificate2? certificate, int port = DefaultPort, IPAddress? bind = null, string? version = null)
    {
        var time = host.GetRequiredService<TimeProvider>();
        var loggerFactory = host.GetRequiredService<ILoggerFactory>();
        var devices = new RemoteDeviceRegistry(host.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Persistence.VernukoDbContext>>(), time);
        var sessions = new RemoteSessionHost(() => RemoteSessionServices.Create(host), time, loggerFactory.CreateLogger<RemoteSessionHost>());
        var dispatcher = new RpcDispatcher(loggerFactory.CreateLogger<RpcDispatcher>());
        var fingerprint = certificate is null ? RemotePairing.PlainFingerprint : RemotePairing.Fingerprint(certificate);
        var license = host.GetRequiredService<ILicenseService>();

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(loggerFactory);
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Limits.MaxRequestBodySize = 40 * 1024 * 1024; // customer files up to 25 MiB travel as base64 (×4/3) inside JSON
            var address = bind ?? (certificate is null ? IPAddress.Loopback : IPAddress.Any);
            if (certificate is null && !IPAddress.IsLoopback(address))
            {
                throw new InvalidOperationException("Without TLS the server may only listen on the loopback address.");
            }

            k.Listen(address, port, o =>
            {
                if (certificate is not null)
                {
                    o.UseHttps(certificate);
                }
            });
        });
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("pair", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "-",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "-",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        var app = builder.Build();
        var server = new RemoteHostServer(app, fingerprint, sessions, devices, time);
        app.UseRateLimiter();

        app.Use(async (http, next) =>
        {
            if (!http.Request.Path.StartsWithSegments("/remote/hello") && http.Request.Headers[VersionHeader].ToString() is var theirs && theirs != ProtocolVersion)
            {
                await Fail(409, "remote.version",
                    $"Bu bilgisayardaki Vernuko sürümü ({(theirs.Length == 0 ? "eski" : theirs)}) ana bilgisayardakiyle ({ProtocolVersion}) aynı değil. İki bilgisayarı da aynı sürüme güncelleyin (Ayarlar › Güncellemeler).")
                    .ExecuteAsync(http);
                return;
            }

            await next(http);
        });

        app.MapGet("/remote/hello", () => Results.Json(new HelloResponse("vernuko", version ?? "0"), RpcJson.Options));

        app.MapPost("/remote/pair", async (HttpContext http) =>
        {
            var r = await http.Request.ReadFromJsonAsync<PairRequest>(RpcJson.Options);
            if (r is null || string.IsNullOrWhiteSpace(r.Nonce) || r.Nonce.Length > 100 || string.IsNullOrWhiteSpace(r.Proof))
            {
                return Fail(400, "remote.bad_request", "İstek okunamadı.");
            }

            await server._pairGate.WaitAsync(http.RequestAborted);
            try
            {
                var code = server.CurrentCode();
                if (code is null)
                {
                    return Fail(403, "remote.no_code", "Ana bilgisayarda geçerli bir bağlantı kodu yok. Ana bilgisayarda Ayarlar › Çoklu bilgisayar › \"Bağlantı kodu göster\" ile yeni kod alın.");
                }

                if (!RemotePairing.Equal(RemotePairing.Proof(code.Code, "client", fingerprint, r.Nonce), r.Proof))
                {
                    server.PairingFailed();
                    return Fail(403, "remote.bad_code", "Bağlantı kodu hatalı (veya araya başka bir cihaz girmiş olabilir). Kodu ana bilgisayarın ekranından tekrar okuyun.");
                }

                if ((await devices.ListAsync()).Count >= await AllowedDevicesAsync(license))
                {
                    return Fail(409, "remote.device_limit", "Lisansınız bu kadar bilgisayara izin veriyor. Kullanılmayan bir bilgisayarı ana bilgisayardaki listeden kaldırın veya lisansınızı yükseltin.");
                }

                var (id, key) = await devices.AddAsync(string.IsNullOrWhiteSpace(r.DeviceName) ? "Bilgisayar" : r.DeviceName.Trim());
                server.ConsumeCode();
                return Results.Json(new PairResponse(id, key, RemotePairing.Proof(code.Code, "host", fingerprint, r.Nonce, id)), RpcJson.Options);
            }
            finally
            {
                server._pairGate.Release();
            }
        }).RequireRateLimiting("pair");

        app.MapPost("/remote/login", async (HttpContext http) =>
        {
            if (await server.DeviceAsync(http) is not { } device)
            {
                return Fail(401, "remote.device", "Bu bilgisayar ana bilgisayarla eşleşmemiş veya eşleşmesi kaldırılmış.");
            }

            var r = await http.Request.ReadFromJsonAsync<LoginRequest>(RpcJson.Options);
            var login = await sessions.LoginAsync(device, r?.Username ?? string.Empty, r?.Password ?? string.Empty, http.RequestAborted);
            return login.IsSuccess ? Results.Json(login.Value, RpcJson.Options) : Fail(403, login.Error.Code, login.Error.Message);
        }).RequireRateLimiting("login");

        app.MapPost("/remote/lock", async (HttpContext http) =>
        {
            if (await server.DeviceAsync(http) is { } && Token(http) is { } token)
            {
                sessions.Lock(token);
            }

            return Results.NoContent();
        });

        app.MapPost("/remote/logout", async (HttpContext http) =>
        {
            if (await server.DeviceAsync(http) is { } && Token(http) is { } token)
            {
                sessions.End(token);
            }

            return Results.NoContent();
        });

        app.MapPost("/rpc/{contract}/{method}", async (string contract, string method, HttpContext http) =>
        {
            if (await server.DeviceAsync(http) is not { } device)
            {
                return Fail(401, "remote.device", "Bu bilgisayar ana bilgisayarla eşleşmemiş veya eşleşmesi kaldırılmış.");
            }

            var services = sessions.Find(Token(http), device)
                ?? (Token(http) is null && RpcContracts.BeforeLogin.Contains(contract + "/" + method) ? sessions.SignedOut : null);
            if (services is null)
            {
                return Fail(401, "remote.session", "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.");
            }

            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, http.RequestAborted);
            var reply = await dispatcher.InvokeAsync(contract, method, ms.ToArray(), services, http.RequestAborted);
            http.Response.StatusCode = reply.Status;
            return Results.Bytes(reply.Body, "application/json");
        });

        await app.StartAsync();
        var bound = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        server.Address = new Uri(bound.Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal).Replace("[::]", "127.0.0.1", StringComparison.Ordinal));
        return server;
    }

    /// <summary>A fresh one-time code, valid for 10 minutes; showing a new one invalidates the old.</summary>
    public PairingCode NewPairingCode()
    {
        lock (_codeLock)
        {
            _failedPairings = 0;
            _code = new PairingCode(RemotePairing.NewCode(), Time.GetUtcNow().UtcDateTime.AddMinutes(10));
            return _code;
        }
    }

    public async Task RemoveDeviceAsync(string deviceId)
    {
        await Devices.RemoveAsync(deviceId);
        Sessions.EndDevice(deviceId);
    }

    public async ValueTask DisposeAsync()
    {
        Sessions.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private PairingCode? CurrentCode()
    {
        lock (_codeLock)
        {
            return _code is { } c && c.ExpiresAtUtc > Time.GetUtcNow().UtcDateTime ? c : null;
        }
    }

    /// <summary>Five wrong proofs burn the code (guessing is not possible within its lifetime).</summary>
    private void PairingFailed()
    {
        lock (_codeLock)
        {
            if (++_failedPairings >= 5)
            {
                _code = null;
            }
        }
    }

    private void ConsumeCode()
    {
        lock (_codeLock)
        {
            _code = null;
        }
    }

    private async Task<string?> DeviceAsync(HttpContext http)
    {
        string? id = http.Request.Headers[DeviceIdHeader];
        if (!await Devices.VerifyAsync(id, http.Request.Headers[DeviceKeyHeader]))
        {
            return null;
        }

        await Devices.TouchAsync(id!);
        return id;
    }

    private static string? Token(HttpContext http) =>
        http.Request.Headers.Authorization.ToString() is { } h && h.StartsWith("Bearer ", StringComparison.Ordinal) ? h[7..] : null;

    private static IResult Fail(int status, string code, string message) =>
        Results.Json(new RpcError(code, message), RpcJson.Options, statusCode: status);
}
