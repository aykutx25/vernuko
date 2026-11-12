using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vernuko.Core;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Common;

public interface ILicenseGate
{
    bool CanWrite { get; }
}

/// <summary>
/// Shared plumbing for use cases: permission checks, license read-only gate, short-lived DbContext per operation,
/// an IMMEDIATE (write-locked) SQLite transaction for every write, and translation of database guard errors.
/// </summary>
public abstract class AppServiceBase(AppServiceDeps deps)
{
    protected IDbContextFactory<VernukoDbContext> Factory => deps.Factory;

    protected UserSession Session => deps.Session;

    protected AuditWriter Audit => deps.Audit;

    protected TimeProvider Time => deps.Time;

    protected DateTime UtcNow => deps.Time.GetUtcNow().UtcDateTime;

    protected Guid? UserId => deps.Session.UserId is { } id && id != Guid.Empty ? id : null;

    protected Result Authorize(Permission permission) =>
        !Session.IsAuthenticated || Session.IsLocked ? Error.Forbidden : Session.Has(permission) ? Result.Success() : Error.Forbidden;

    protected async Task<Result<T>> ReadAsync<T>(Permission permission, Func<VernukoDbContext, Task<Result<T>>> work, CancellationToken ct = default)
    {
        var auth = Authorize(permission);
        if (auth.IsFailure)
        {
            await RecordDeniedAsync(permission, ct);
            return auth.Error;
        }

        await using var db = await Factory.CreateDbContextAsync(ct);
        return await work(db);
    }

    protected Task<Result<T>> WriteAsync<T>(Permission permission, Func<VernukoDbContext, Task<Result<T>>> work, CancellationToken ct = default) =>
        WriteCoreAsync(permission, requireWritableLicense: true, work, ct);

    /// <summary>Writes allowed even in read-only license mode (backups, restore, own password, license import).</summary>
    protected Task<Result<T>> WriteAlwaysAsync<T>(Permission? permission, Func<VernukoDbContext, Task<Result<T>>> work, CancellationToken ct = default) =>
        WriteCoreAsync(permission, requireWritableLicense: false, work, ct);

    private async Task<Result<T>> WriteCoreAsync<T>(Permission? permission, bool requireWritableLicense, Func<VernukoDbContext, Task<Result<T>>> work, CancellationToken ct)
    {
        if (permission is { } p)
        {
            var auth = Authorize(p);
            if (auth.IsFailure)
            {
                await RecordDeniedAsync(p, ct);
                return auth.Error;
            }
        }

        if (requireWritableLicense && !deps.License.CanWrite)
        {
            return Error.ReadOnlyMode;
        }

        await using var db = await Factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work(db);
            if (result.IsFailure)
            {
                await tx.RollbackAsync(ct);
                return result;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Error.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException se && se.Message.Contains(DatabaseTriggers.OverlapError, StringComparison.Ordinal))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return AppErrors.BookingOverlap;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 } se && se.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return AppErrors.Duplicate;
        }
    }

    /// <summary>Denied access to sensitive areas is recorded in the audit log.</summary>
    private async Task RecordDeniedAsync(Permission permission, CancellationToken ct)
    {
        if (!Session.IsAuthenticated || permission is not (Permission.ReportsFinancial or Permission.PaymentsView or Permission.PaymentsReverse
            or Permission.CustomerSensitiveDataView or Permission.CustomersExport or Permission.BackupsRestore or Permission.UsersManage or Permission.AuditLogView))
        {
            return;
        }

        try
        {
            await using var db = await Factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Audit.AppendAsync(db, "access.denied", "Permission", null, permission.ToString(), ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Auditing a denial must never crash the caller.
        }
    }
}

public sealed class AppServiceDeps(IDbContextFactory<VernukoDbContext> factory, UserSession session, AuditWriter audit, TimeProvider time, ILicenseGate license)
{
    public IDbContextFactory<VernukoDbContext> Factory { get; } = factory;

    public UserSession Session { get; } = session;

    public AuditWriter Audit { get; } = audit;

    public TimeProvider Time { get; } = time;

    public ILicenseGate License { get; } = license;
}

public static class AppErrors
{
    public static readonly Error BookingOverlap = Error.Conflict("booking_overlap", "Bu saat aralığında seçilen personel, oda veya cihaz başka bir randevuya ayrılmış.");
    public static readonly Error Duplicate = Error.Conflict("duplicate", "Bu kayıt zaten oluşturulmuş.");
    public static readonly Error SetupNotCompleted = new("setup.not_completed", "Kurulum henüz tamamlanmamış.");
    public static readonly Error SetupAlreadyCompleted = new("setup.completed", "Kurulum zaten tamamlanmış.");
    public static readonly Error InvalidCredentials = new("auth.invalid", "Kullanıcı adı veya parola hatalı.");
    public static readonly Error AccountLocked = new("auth.locked", "Çok sayıda hatalı deneme yapıldı. Hesap geçici olarak kilitlendi; birkaç dakika sonra tekrar deneyin.");
    public static readonly Error AccountInactive = new("auth.inactive", "Bu kullanıcı hesabı devre dışı.");
}
