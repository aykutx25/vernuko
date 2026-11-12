using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Core;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public sealed class AuthService(IDbContextFactory<VernukoDbContext> factory, PasswordHasher hasher, TimeProvider time, UserSession session, AuditWriter audit)
{
    public async Task<Result> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var name = (username ?? string.Empty).Trim().ToLowerInvariant();
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == name, ct);
        if (user is null)
        {
            // Same cost as a real verification to avoid username probing via timing.
            hasher.Verify(password ?? string.Empty, DummyHash.Value);
            return AppErrors.InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return AppErrors.AccountInactive;
        }

        if (user.IsLockedOut(now))
        {
            return AppErrors.AccountLocked;
        }

        if (!hasher.Verify(password ?? string.Empty, user.PasswordHash))
        {
            user.RegisterFailedLogin(now);
            await audit.AppendAsAsync(db, user.Id, user.Username, "auth.login_failed", "User", user.Id, $"Hatalı giriş denemesi ({user.FailedLoginCount})", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return user.IsLockedOut(now) ? AppErrors.AccountLocked : AppErrors.InvalidCredentials;
        }

        if (hasher.NeedsRehash(user.PasswordHash))
        {
            user.SetPasswordHash(hasher.Hash(password!));
        }

        user.RegisterSuccessfulLogin(now);
        session.SignIn(user);
        await audit.AppendAsync(db, "auth.login", "User", user.Id, "Giriş yapıldı", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    /// <summary>Unlocks an auto-locked session with the current user's password.</summary>
    public async Task<Result> UnlockAsync(string password, CancellationToken ct = default)
    {
        if (session.UserId is not { } id)
        {
            return AppErrors.InvalidCredentials;
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || !user.IsActive || !hasher.Verify(password ?? string.Empty, user.PasswordHash))
        {
            return AppErrors.InvalidCredentials;
        }

        session.Unlock();
        return Result.Success();
    }

    public async Task<Result> ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (session.UserId is not { } id || id == Guid.Empty)
        {
            return Error.Forbidden;
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FirstAsync(u => u.Id == id, ct);
        if (!hasher.Verify(currentPassword ?? string.Empty, user.PasswordHash))
        {
            return AppErrors.InvalidCredentials;
        }

        var policy = PasswordPolicy.Validate(newPassword, user.Username);
        if (policy.IsFailure)
        {
            return policy;
        }

        user.SetPasswordHash(hasher.Hash(newPassword));
        await audit.AppendAsync(db, "auth.password_changed", "User", user.Id, "Parola değiştirildi", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    public void Lock() => session.Lock();

    public void Logout() => session.SignOut();

    private static class DummyHash
    {
        public static readonly string Value = new PasswordHasher().Hash("vernuko-dummy-password");
    }
}

public sealed class UserAdminService(AppServiceDeps deps, PasswordHasher hasher) : AppServiceBase(deps)
{
    public Task<Result<IReadOnlyList<UserSummary>>> ListAsync(CancellationToken ct = default) =>
        ReadAsync<IReadOnlyList<UserSummary>>(Permission.UsersManage, async db =>
            (await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(ct))
                .Select(u => new UserSummary(u.Id, u.Username, u.DisplayName, u.Role, u.IsActive, u.LastLoginAtUtc))
                .ToList(), ct);

    public Task<Result<Guid>> CreateAsync(string username, string displayName, string password, SystemRole role, CancellationToken ct = default) =>
        WriteAsync<Guid>(Permission.UsersManage, async db =>
        {
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3 || username.Any(char.IsWhiteSpace))
            {
                return Error.Validation("user.username", "Kullanıcı adı en az 3 karakter olmalı ve boşluk içermemelidir.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Error.Validation("user.display_name", "Ad soyad zorunludur.");
            }

            var policy = PasswordPolicy.Validate(password, username);
            if (policy.IsFailure)
            {
                return policy.Error;
            }

            if (role == SystemRole.Owner && Session.Role != SystemRole.Owner)
            {
                return Error.Forbidden;
            }

            var normalized = username.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Username == normalized, ct))
            {
                return Error.Validation("user.exists", "Bu kullanıcı adı zaten kullanılıyor.");
            }

            var user = new UserAccount(normalized, displayName, hasher.Hash(password), role);
            db.Users.Add(user);
            await Audit.AppendAsync(db, "user.created", "User", user.Id, $"Kullanıcı oluşturuldu, rol {role}", ct);
            return user.Id;
        }, ct);

    public Task<Result<bool>> UpdateAsync(Guid userId, string displayName, SystemRole role, bool isActive, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.UsersManage, async db =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return Error.NotFound("Kullanıcı");
            }

            if ((user.Role == SystemRole.Owner || role == SystemRole.Owner) && Session.Role != SystemRole.Owner)
            {
                return Error.Forbidden;
            }

            if (user.Role == SystemRole.Owner && (role != SystemRole.Owner || !isActive)
                && await db.Users.CountAsync(u => u.Role == SystemRole.Owner && u.IsActive, ct) <= 1)
            {
                return Error.Validation("user.last_owner", "En az bir aktif işletme sahibi hesabı bulunmalıdır.");
            }

            user.Rename(displayName);
            user.ChangeRole(role);
            if (isActive)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }

            await Audit.AppendAsync(db, "user.updated", "User", user.Id, $"Rol {role}, aktif {isActive}", ct);
            return true;
        }, ct);

    public Task<Result<bool>> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default) =>
        WriteAsync<bool>(Permission.UsersManage, async db =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null)
            {
                return Error.NotFound("Kullanıcı");
            }

            if (user.Role == SystemRole.Owner && Session.Role != SystemRole.Owner)
            {
                return Error.Forbidden;
            }

            var policy = PasswordPolicy.Validate(newPassword, user.Username);
            if (policy.IsFailure)
            {
                return policy.Error;
            }

            user.SetPasswordHash(hasher.Hash(newPassword));
            user.ClearLock();
            await Audit.AppendAsync(db, "user.password_reset", "User", user.Id, "Parola yönetici tarafından sıfırlandı", ct);
            return true;
        }, ct);
}
