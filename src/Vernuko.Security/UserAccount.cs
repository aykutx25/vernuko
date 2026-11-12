using Vernuko.Core;

namespace Vernuko.Security;

public sealed class UserAccount : Entity
{
    public const int MaxFailedAttemptsBeforeLock = 5;

    private UserAccount()
    {
    }

    public UserAccount(string username, string displayName, string passwordHash, SystemRole role)
    {
        Username = Guard.NotBlank(username).Trim().ToLowerInvariant();
        DisplayName = Guard.NotBlank(displayName).Trim();
        PasswordHash = Guard.NotBlank(passwordHash);
        Role = role;
        IsActive = true;
    }

    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public SystemRole Role { get; private set; }

    /// <summary>Extra permissions on top of the role defaults (comma separated names).</summary>
    public string GrantedPermissions { get; private set; } = string.Empty;

    /// <summary>Permissions removed from the role defaults (comma separated names).</summary>
    public string DeniedPermissions { get; private set; } = string.Empty;

    public Guid? StaffMemberId { get; private set; }

    public bool IsActive { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public IReadOnlySet<Permission> EffectivePermissions()
    {
        var set = new HashSet<Permission>(RoleDefaults.For(Role));
        foreach (var p in ParsePermissions(GrantedPermissions))
        {
            set.Add(p);
        }

        foreach (var p in ParsePermissions(DeniedPermissions))
        {
            set.Remove(p);
        }

        return set;
    }

    public void SetPermissionOverrides(IEnumerable<Permission> granted, IEnumerable<Permission> denied)
    {
        GrantedPermissions = string.Join(',', granted.Distinct().Order());
        DeniedPermissions = string.Join(',', denied.Distinct().Order());
    }

    public void ChangeRole(SystemRole role) => Role = role;

    public void LinkStaff(Guid? staffMemberId) => StaffMemberId = staffMemberId;

    public void Rename(string displayName) => DisplayName = Guard.NotBlank(displayName).Trim();

    public void SetPasswordHash(string hash) => PasswordHash = Guard.NotBlank(hash);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public bool IsLockedOut(DateTime nowUtc) => LockedUntilUtc is { } until && until > nowUtc;

    public void RegisterFailedLogin(DateTime nowUtc)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedAttemptsBeforeLock)
        {
            // 1, 2, 4, 8 ... minutes, capped at one hour.
            var exponent = Math.Min(FailedLoginCount - MaxFailedAttemptsBeforeLock, 6);
            LockedUntilUtc = nowUtc.AddMinutes(Math.Pow(2, exponent));
        }
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockedUntilUtc = null;
        LastLoginAtUtc = nowUtc;
    }

    public void ClearLock()
    {
        FailedLoginCount = 0;
        LockedUntilUtc = null;
    }

    private static IEnumerable<Permission> ParsePermissions(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<Permission>(s, out var p) ? (Permission?)p : null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value);
}
