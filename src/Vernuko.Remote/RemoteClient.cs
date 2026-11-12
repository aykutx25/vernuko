using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Licensing;

namespace Vernuko.Remote;

/// <summary>Where the main computer is and how this computer proves it was paired (stored DPAPI-protected).</summary>
public sealed record RemoteEndpoint(Uri Address, string Fingerprint, string DeviceId, string DeviceKey);

public static class RemoteErrors
{
    public static readonly Error Unavailable = new("remote.unavailable", "Ana bilgisayara ulaşılamıyor. Ana bilgisayarın açık, Vernuko'nun çalışıyor ve iki bilgisayarın aynı ağda olduğunu kontrol edin.");

    public static readonly Error SessionEnded = new("remote.session", "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.");

    public static readonly Error Certificate = new("remote.certificate", "Ana bilgisayarın kimliği eşleştirmedeki ile aynı değil. Bilgisayarı yeniden eşleştirin; ağda başka bir cihaz ana bilgisayar gibi davranıyor olabilir.");
}

/// <summary>
/// Connected computer → main computer over HTTPS. The main computer's certificate is accepted only if its
/// fingerprint equals the one pinned at pairing (no certificate authority involved).
/// </summary>
public sealed class RemoteConnection : IRpcChannel, IDisposable
{
    private readonly HttpClient _http;
    private string? _token;

    public RemoteConnection(RemoteEndpoint endpoint)
    {
        Endpoint = endpoint;
        _http = CreateClient(endpoint.Address, cert => cert is not null && RemotePairing.Equal(RemotePairing.Fingerprint(cert), endpoint.Fingerprint), endpoint.Fingerprint);
        _http.DefaultRequestHeaders.Add(RemoteHostServer.DeviceIdHeader, endpoint.DeviceId);
        _http.DefaultRequestHeaders.Add(RemoteHostServer.DeviceKeyHeader, endpoint.DeviceKey);
    }

    public RemoteEndpoint Endpoint { get; }

    public bool HasSession => _token is not null;

    /// <summary>Raised when the main computer no longer knows the session (restart, timeout, device removed).</summary>
    public event EventHandler? SessionEnded;

    /// <summary>Pairs with the main computer using the code shown on its screen; returns what to store.</summary>
    public static async Task<Result<RemoteEndpoint>> PairAsync(Uri address, string code, string deviceName, CancellationToken ct = default)
    {
        string? seen = null;
        using var http = CreateClient(address, cert =>
        {
            var fp = cert is null ? null : RemotePairing.Fingerprint(cert);
            seen ??= fp;
            return fp is not null && fp == seen;
        }, null);
        try
        {
            using (var hello = await http.GetAsync(new Uri(address, "remote/hello"), ct))
            {
                if (!hello.IsSuccessStatusCode)
                {
                    return new Error("remote.not_vernuko", "Bu adreste Vernuko ana bilgisayarı bulunamadı.");
                }
            }

            var fingerprint = address.Scheme == Uri.UriSchemeHttp ? RemotePairing.PlainFingerprint : seen;
            if (fingerprint is null)
            {
                return RemoteErrors.Certificate;
            }

            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            using var response = await http.PostAsJsonAsync(new Uri(address, "remote/pair"),
                new PairRequest(deviceName, nonce, RemotePairing.Proof(code, "client", fingerprint, nonce)), RpcJson.Options, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await ErrorAsync(response, ct);
            }

            var pair = await response.Content.ReadFromJsonAsync<PairResponse>(RpcJson.Options, ct);
            if (pair is null || !RemotePairing.Equal(RemotePairing.Proof(code, "host", fingerprint, nonce, pair.DeviceId), pair.HostProof))
            {
                return RemoteErrors.Certificate;
            }

            return new RemoteEndpoint(address, fingerprint, pair.DeviceId, pair.DeviceKey);
        }
        catch (HttpRequestException)
        {
            return RemoteErrors.Unavailable;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return RemoteErrors.Unavailable;
        }
    }

    public async Task<Result<RemoteUser>> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var (body, error) = await SendAsync("remote/login", JsonSerializer.SerializeToUtf8Bytes(new LoginRequest(username, password), RpcJson.Options), ct);
        if (error is not null)
        {
            return error;
        }

        var login = JsonSerializer.Deserialize<RemoteLogin>(body!, RpcJson.Options)!;
        _token = login.Token;
        return login.User;
    }

    public async Task LockAsync()
    {
        _ = await SendAsync("remote/lock", [], CancellationToken.None);
    }

    public async Task LogoutAsync()
    {
        if (_token is not null)
        {
            _ = await SendAsync("remote/logout", [], CancellationToken.None);
        }

        _token = null;
    }

    public Task<(byte[]? Body, Error? Error)> CallAsync(string contract, string method, byte[] arguments, CancellationToken ct) =>
        SendAsync($"rpc/{contract}/{method}", arguments, ct);

    public void Dispose() => _http.Dispose();

    private async Task<(byte[]? Body, Error? Error)> SendAsync(string path, byte[] body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                return (bytes, null);
            }

            var error = Parse(bytes) ?? new Error("remote.error", $"Ana bilgisayar isteği reddetti (HTTP {(int)response.StatusCode}).");
            if (response.StatusCode == HttpStatusCode.Unauthorized && error.Code is "remote.session" or "remote.device")
            {
                // Only a session that existed can end (calls before signing in just fail, no login loop).
                var hadSession = _token is not null;
                _token = null;
                if (hadSession)
                {
                    SessionEnded?.Invoke(this, EventArgs.Empty);
                }
            }

            return (null, error);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            return (null, RemoteErrors.Certificate);
        }
        catch (HttpRequestException)
        {
            return (null, RemoteErrors.Unavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, RemoteErrors.Unavailable);
        }
    }

    private static HttpClient CreateClient(Uri address, Func<System.Security.Cryptography.X509Certificates.X509Certificate2?, bool> accept, string? pinned)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, cert, _, _) => accept(cert as System.Security.Cryptography.X509Certificates.X509Certificate2),
            },
        };
        if (address.Scheme == Uri.UriSchemeHttp && !(address.IsLoopback && (pinned is null || pinned == RemotePairing.PlainFingerprint)))
        {
            throw new ArgumentException("Connected computers use HTTPS (plain HTTP only for loopback tests).", nameof(address));
        }

        return new HttpClient(handler) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(60) };
    }

    private static async Task<Error> ErrorAsync(HttpResponseMessage response, CancellationToken ct) =>
        Parse(await response.Content.ReadAsByteArrayAsync(ct)) ?? new Error("remote.error", $"Ana bilgisayar isteği reddetti (HTTP {(int)response.StatusCode}).");

    private static Error? Parse(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<RpcError>(body, RpcJson.Options) is { Code: { } code, Message: { } message } ? new Error(code, message) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Sign-in on a connected computer: checked by the main computer; the local session is a copy for the screens.</summary>
public sealed class RemoteAuthService(RemoteConnection connection, UserSession session, IAuthService remote) : IAuthService
{
    public async Task<Result> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await connection.LoginAsync(username, password, ct);
        if (user.IsFailure)
        {
            return user.Error;
        }

        session.SignInRemote(user.Value);
        return Result.Success();
    }

    public async Task<Result> UnlockAsync(string password, CancellationToken ct = default)
    {
        var r = await remote.UnlockAsync(password, ct);
        if (r.IsSuccess)
        {
            session.Unlock();
        }

        return r;
    }

    public Task<Result> ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) =>
        remote.ChangeOwnPasswordAsync(currentPassword, newPassword, ct);

    public void Lock()
    {
        session.Lock();
        _ = connection.LockAsync();
    }

    public void Logout()
    {
        session.SignOut();
        _ = connection.LogoutAsync();
    }
}

/// <summary>The main computer's license as seen from a connected computer (cached for the screens).</summary>
public sealed class RemoteLicenseService(ILicenseService remote) : ILicenseService, ILicenseGate
{
    public LicenseStatus Current { get; private set; } = new(LicenseState.Trial, AccessMode.Full, null, null, false, "Lisans ana bilgisayardan okunuyor…");

    public bool CanWrite => Current.CanWrite;

    public event EventHandler? Changed;

    public async Task<LicenseStatus> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            Current = await remote.RefreshAsync(ct);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (RemoteCallException)
        {
        }

        return Current;
    }

    public Task<Result<LicenseStatus>> ImportAsync(string signedLicenseJson, CancellationToken ct = default) => remote.ImportAsync(signedLicenseJson, ct);

    public Task<Result<LicenseStatus>> StoreFromServerAsync(string signedLicenseJson, bool activation, CancellationToken ct = default) => remote.StoreFromServerAsync(signedLicenseJson, activation, ct);

    public Task<LicenseDocument?> GetDocumentAsync(CancellationToken ct = default) => remote.GetDocumentAsync(ct);

    public Task<LicenseStatus> RemoveLocalAsync(string reason, CancellationToken ct = default) => remote.RemoveLocalAsync(reason, ct);

    public Task<LicenseStatus> AdoptServerTrialStartAsync(DateTime serverStartUtc, CancellationToken ct = default) => remote.AdoptServerTrialStartAsync(serverStartUtc, ct);
}

public static class RemoteClientSetup
{
    /// <summary>Services of a connected computer: every contract goes to the main computer.</summary>
    public static IServiceCollection AddVernukoRemoteClient(this IServiceCollection services, RemoteConnection connection)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(connection);
        services.AddSingleton<IRpcChannel>(connection);
        services.AddSingleton<UserSession>();
        var create = typeof(RpcProxy).GetMethod(nameof(RpcProxy.Create))!;
        foreach (var contract in RpcContracts.All.Where(c => c != typeof(IAuthService) && c != typeof(ILicenseService)))
        {
            var proxy = create.MakeGenericMethod(contract).Invoke(null, [connection])!;
            services.AddSingleton(contract, proxy);
        }

        services.AddSingleton<IAuthService>(sp => new RemoteAuthService(connection, sp.GetRequiredService<UserSession>(), RpcProxy.Create<IAuthService>(connection)));
        services.AddSingleton<RemoteLicenseService>(_ => new RemoteLicenseService(RpcProxy.Create<ILicenseService>(connection)));
        services.AddSingleton<ILicenseService>(sp => sp.GetRequiredService<RemoteLicenseService>());
        services.AddSingleton<ILicenseGate>(sp => sp.GetRequiredService<RemoteLicenseService>());
        return services;
    }
}
