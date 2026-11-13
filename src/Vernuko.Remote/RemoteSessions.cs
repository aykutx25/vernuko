using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Security;

namespace Vernuko.Remote;

/// <summary>
/// Builds the services for one remote session on the main computer: its own UserSession (so two people on two
/// computers never share a login), the same database file, and the main computer's license, secret protector,
/// clock and logging.
/// </summary>
public static class RemoteSessionServices
{
    public static ServiceProvider Create(IServiceProvider host)
    {
        var env = host.GetRequiredService<IAppEnvironment>();
        var services = new ServiceCollection();
        services.AddSingleton(env);
        services.AddSingleton(host.GetRequiredService<IDeviceIdentity>());
        services.AddSingleton(host.GetRequiredService<ISecretProtector>());
        services.AddSingleton(host.GetRequiredService<TimeProvider>());
        services.AddSingleton(host.GetRequiredService<LicensingOptions>());
        services.AddSingleton(host.GetRequiredService<PasswordHasher>());
        services.AddSingleton(host.GetRequiredService<IMessageTransportFactory>());
        services.AddSingleton(host.GetRequiredService<LicenseService>());
        services.AddSingleton(host.GetRequiredService<ILoggerFactory>());
        services.AddLogging();
        services.AddVernukoApplication(env.DatabasePath);
        return services.BuildServiceProvider();
    }
}

public sealed record RemoteLogin(string Token, RemoteUser User);

/// <summary>Signed-in remote sessions on the main computer (memory only; a restart signs everyone out).</summary>
public sealed class RemoteSessionHost(Func<ServiceProvider> createServices, TimeProvider time, ILogger<RemoteSessionHost> logger) : IDisposable
{
    /// <summary>Idle sessions end after this; the connected computer then shows the login screen.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Lazy<ServiceProvider> _signedOut = new(createServices);

    /// <summary>Services with nobody signed in, for the few calls allowed before login (permission checks still apply).</summary>
    public IServiceProvider SignedOut => _signedOut.Value;

    public int Count => _sessions.Count;

    public async Task<Result<RemoteLogin>> LoginAsync(string deviceId, string username, string password, CancellationToken ct)
    {
        var services = createServices();
        var login = await services.GetRequiredService<IAuthService>().LoginAsync(username, password, ct);
        var user = services.GetRequiredService<UserSession>().ToRemoteUser();
        if (login.IsFailure || user is null)
        {
            await services.DisposeAsync();
            return login.IsFailure ? login.Error : AppErrors.InvalidCredentials;
        }

        // A connected computer runs one Vernuko at a time: its earlier session (the app was closed or restarted without
        // signing out) is ended, and idle sessions of other computers are swept, so service containers do not pile up.
        EndDevice(deviceId);
        SweepIdle();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _sessions[token] = new Session(deviceId, services, time.GetUtcNow().UtcDateTime);
        logger.LogInformation("Remote session started for device {Device}", deviceId);
        return new RemoteLogin(token, user);
    }

    /// <summary>The session's services, or null when the token is unknown, expired or belongs to another device.</summary>
    public IServiceProvider? Find(string? token, string deviceId)
    {
        if (token is null || !_sessions.TryGetValue(token, out var s) || s.DeviceId != deviceId)
        {
            return null;
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (now - s.LastSeenUtc > IdleTimeout)
        {
            End(token);
            return null;
        }

        s.LastSeenUtc = now;
        return s.Services;
    }

    public void Lock(string token)
    {
        if (_sessions.TryGetValue(token, out var s))
        {
            s.Services.GetRequiredService<UserSession>().Lock();
        }
    }

    public void End(string token)
    {
        if (_sessions.TryRemove(token, out var s))
        {
            s.Services.Dispose();
        }
    }

    private void SweepIdle()
    {
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var (token, s) in _sessions)
        {
            if (now - s.LastSeenUtc > IdleTimeout)
            {
                End(token);
            }
        }
    }

    /// <summary>Removing a paired computer ends its sessions immediately.</summary>
    public void EndDevice(string deviceId)
    {
        foreach (var (token, s) in _sessions)
        {
            if (s.DeviceId == deviceId)
            {
                End(token);
            }
        }
    }

    public void Dispose()
    {
        foreach (var token in _sessions.Keys)
        {
            End(token);
        }

        if (_signedOut.IsValueCreated)
        {
            _signedOut.Value.Dispose();
        }
    }

    private sealed class Session(string deviceId, ServiceProvider services, DateTime lastSeenUtc)
    {
        public string DeviceId { get; } = deviceId;

        public ServiceProvider Services { get; } = services;

        public DateTime LastSeenUtc { get; set; } = lastSeenUtc;
    }
}
