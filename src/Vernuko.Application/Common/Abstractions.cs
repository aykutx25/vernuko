using Vernuko.Core;
using Vernuko.Security;

namespace Vernuko.Application.Common;

/// <summary>Protects small secrets at rest (Windows DPAPI in production).</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);

    byte[] Unprotect(byte[] protectedData);
}

/// <summary>File-system locations and version of the running app.</summary>
public interface IAppEnvironment
{
    string DatabasePath { get; }

    string DataDirectory { get; }

    string SafetyDirectory { get; }

    string TempDirectory { get; }

    string DefaultBackupDirectory { get; }

    /// <summary>Encrypted customer attachments (photos, signed consent scans).</summary>
    string AttachmentsDirectory { get; }

    string AppVersion { get; }

    bool IsDemo { get; }
}

/// <summary>Privacy-preserving machine identifier used for license activation.</summary>
public interface IDeviceIdentity
{
    string DeviceHash { get; }
}

/// <summary>Who is signed in, as sent from the main computer to a connected one.</summary>
public sealed record RemoteUser(Guid Id, string Username, string DisplayName, SystemRole Role, Guid? StaffMemberId, IReadOnlyList<Permission> Permissions);

/// <summary>The signed-in user of this desktop session.</summary>
public sealed class UserSession
{
    private IReadOnlySet<Permission> _permissions = new HashSet<Permission>();

    public Guid? UserId { get; private set; }

    public string? Username { get; private set; }

    public string? DisplayName { get; private set; }

    public SystemRole? Role { get; private set; }

    public Guid? StaffMemberId { get; private set; }

    public bool IsAuthenticated => UserId is not null;

    public bool IsLocked { get; private set; }

    public event EventHandler? Changed;

    public bool Has(Permission permission) => IsAuthenticated && !IsLocked && _permissions.Contains(permission);

    public void SignIn(UserAccount user)
    {
        UserId = user.Id;
        Username = user.Username;
        DisplayName = user.DisplayName;
        Role = user.Role;
        StaffMemberId = user.StaffMemberId;
        _permissions = user.EffectivePermissions();
        IsLocked = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The user signed in on the main computer (connected computer): a copy for showing/hiding screens only.</summary>
    public void SignInRemote(RemoteUser user)
    {
        UserId = user.Id;
        Username = user.Username;
        DisplayName = user.DisplayName;
        Role = user.Role;
        StaffMemberId = user.StaffMemberId;
        _permissions = user.Permissions.ToHashSet();
        IsLocked = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public RemoteUser? ToRemoteUser() => UserId is { } id && Username is { } name && Role is { } role
        ? new RemoteUser(id, name, DisplayName ?? name, role, StaffMemberId, _permissions.Order().ToList())
        : null;

    /// <summary>Used by background/system operations (setup wizard, scheduled backup) — never by UI actions.</summary>
    public void SignInSystem(string name, IReadOnlySet<Permission> permissions)
    {
        UserId = Guid.Empty;
        Username = name;
        DisplayName = name;
        Role = null;
        _permissions = permissions;
        IsLocked = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Lock()
    {
        IsLocked = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Unlock()
    {
        IsLocked = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        UserId = null;
        Username = null;
        DisplayName = null;
        Role = null;
        StaffMemberId = null;
        _permissions = new HashSet<Permission>();
        IsLocked = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public enum ComputerRole
{
    /// <summary>One computer, local database (the default).</summary>
    Standalone,

    /// <summary>Local database and serving connected computers on the local network.</summary>
    Host,

    /// <summary>No local data: every call goes to the main computer.</summary>
    Client,
}

public sealed record PairedComputer(string Id, string Name, DateTime PairedAtUtc, DateTime? LastSeenUtc);

public sealed record ConnectionCode(string Code, DateTime ExpiresAtUtc);

/// <summary>Multi-computer mode (Faz 7): main computer + connected computers on the local network.</summary>
public interface IMultiComputer
{
    ComputerRole Role { get; }

    /// <summary>Main computer: the addresses connected computers can use; connected computer: the main computer.</summary>
    IReadOnlyList<string> Addresses { get; }

    /// <summary>Connected computer: the main computer ended this session (restart, timeout, removed).</summary>
    event EventHandler? SessionEnded;

    Task<Result> EnableHostAsync(CancellationToken ct = default);

    Task<Result> DisableHostAsync(CancellationToken ct = default);

    /// <summary>Starts the main-computer server at launch when it was enabled before.</summary>
    Task StartIfEnabledAsync(CancellationToken ct = default);

    Result<ConnectionCode> NewConnectionCode();

    Task<IReadOnlyList<PairedComputer>> ListComputersAsync(CancellationToken ct = default);

    Task<Result> RemoveComputerAsync(string id, CancellationToken ct = default);

    /// <summary>Pairs this computer with a main computer; takes effect after restarting Vernuko.</summary>
    Task<Result> PairWithHostAsync(string address, string code, string computerName, CancellationToken ct = default);

    /// <summary>Stops using the main computer; takes effect after restarting Vernuko.</summary>
    Task<Result> ForgetHostAsync(CancellationToken ct = default);
}

/// <summary>Used when the multi-computer module is not part of the build (tests, tools).</summary>
public sealed class StandaloneComputer : IMultiComputer
{
    private static readonly Error NotAvailable = new("remote.not_available", "Çoklu bilgisayar bu sürümde kullanılamıyor.");

    public ComputerRole Role => ComputerRole.Standalone;

    public IReadOnlyList<string> Addresses => [];

    public event EventHandler? SessionEnded
    {
        add { }
        remove { }
    }

    public Task<Result> EnableHostAsync(CancellationToken ct = default) => Task.FromResult<Result>(NotAvailable);

    public Task<Result> DisableHostAsync(CancellationToken ct = default) => Task.FromResult(Result.Success());

    public Task StartIfEnabledAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Result<ConnectionCode> NewConnectionCode() => NotAvailable;

    public Task<IReadOnlyList<PairedComputer>> ListComputersAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PairedComputer>>([]);

    public Task<Result> RemoveComputerAsync(string id, CancellationToken ct = default) => Task.FromResult<Result>(NotAvailable);

    public Task<Result> PairWithHostAsync(string address, string code, string computerName, CancellationToken ct = default) => Task.FromResult<Result>(NotAvailable);

    public Task<Result> ForgetHostAsync(CancellationToken ct = default) => Task.FromResult(Result.Success());
}
