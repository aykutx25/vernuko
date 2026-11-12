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
