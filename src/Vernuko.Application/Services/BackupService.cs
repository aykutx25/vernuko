using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vernuko.Application.Common;
using Vernuko.Backups;
using Vernuko.Core;
using Vernuko.Persistence;
using Vernuko.Security;

namespace Vernuko.Application.Services;

public enum BackupSchedule
{
    Off,
    Daily,
    Weekly,
    Monthly,
}

public sealed record BackupStatus(
    bool KeyConfigured,
    string? KeyId,
    bool RecoveryKeyConfirmed,
    string Folder,
    BackupSchedule Schedule,
    bool OnExit,
    RetentionPolicy Retention,
    DateTime? LastSuccessUtc,
    string? LastFile,
    string? LastError)
{
    public bool AttachmentsIncluded => false;
}

public sealed record BackupListItem(string FileName, DateTime? CreatedAtUtc, BackupTrigger? Trigger, long SizeBytes, string? AppVersion, string? SchemaVersion, bool KeyMatches, string? Problem);

public sealed record BackupRunResult(string FileName, long SizeBytes, TimeSpan Duration, int DeletedOldBackups);

public sealed record BackupVerifyResult(Guid BackupId, DateTime CreatedAtUtc, string AppVersion, string SchemaVersion, long DatabaseBytes, bool AttachmentsIncluded);

public sealed record RestoreResult(string RestoredFrom, DateTime BackupCreatedAtUtc, string SafetyCopyPath, string? SafetyBackupFile);

/// <summary>
/// Local encrypted backups (Phase 1). Cloud targets (Google Drive / OneDrive / Dropbox) plug in as
/// <see cref="IBackupTarget"/> implementations in Phase 4 and are not available yet.
/// </summary>
public sealed class BackupService(
    IDbContextFactory<VernukoDbContext> factory,
    IAppEnvironment env,
    BackupKeyStore keys,
    ISecretProtector protector,
    UserSession session,
    AuditWriter audit,
    TimeProvider time,
    DatabaseInitializer initializer,
    ILogger<BackupService> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<BackupStatus> GetStatusAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var retention = await SettingsStore.GetAsync(db, SettingKeys.BackupRetention, ct);
        return new BackupStatus(
            await SettingsStore.GetAsync(db, SettingKeys.BackupProtectedKek, ct) is not null,
            await SettingsStore.GetAsync(db, SettingKeys.BackupKeyId, ct),
            await SettingsStore.GetAsync(db, SettingKeys.BackupRecoveryKeyConfirmed, ct) == "true",
            await SettingsStore.GetAsync(db, SettingKeys.BackupFolder, ct) ?? env.DefaultBackupDirectory,
            Enum.TryParse<BackupSchedule>(await SettingsStore.GetAsync(db, SettingKeys.BackupSchedule, ct), out var s) ? s : BackupSchedule.Off,
            await SettingsStore.GetAsync(db, SettingKeys.BackupOnExit, ct) == "true",
            retention is null ? RetentionPolicy.Default : JsonSerializer.Deserialize<RetentionPolicy>(retention) ?? RetentionPolicy.Default,
            await SettingsStore.GetUtcAsync(db, SettingKeys.BackupLastSuccessUtc, ct),
            await SettingsStore.GetAsync(db, SettingKeys.BackupLastFile, ct),
            await SettingsStore.GetAsync(db, SettingKeys.BackupLastError, ct));
    }

    public async Task<Result<bool>> ConfigureAsync(string folder, BackupSchedule schedule, bool onExit, RetentionPolicy retention, CancellationToken ct = default)
    {
        if (!session.Has(Permission.BackupsManage))
        {
            return Error.Forbidden;
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            return Error.Validation("backup.folder", "Yedek klasörü seçilmelidir.");
        }

        if (retention.Daily is < 0 or > 365 || retention.Weekly is < 0 or > 104 || retention.Monthly is < 0 or > 120 || retention.Daily + retention.Weekly + retention.Monthly == 0)
        {
            return Error.Validation("backup.retention", "Saklama ayarları geçersiz.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupFolder, Path.GetFullPath(folder.Trim()), now, ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupSchedule, schedule.ToString(), now, ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupOnExit, onExit ? "true" : "false", now, ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupRetention, JsonSerializer.Serialize(retention), now, ct);
        await audit.AppendAsync(db, "backup.configured", "Backup", null, $"Yedekleme: {schedule}, kapanışta {onExit}, saklama {retention.Daily}/{retention.Weekly}/{retention.Monthly}", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>Creates a new recovery key. Backups made with the previous key still need the previous key.</summary>
    public async Task<Result<string>> CreateRecoveryKeyAsync(CancellationToken ct = default)
    {
        if (!session.Has(Permission.BackupsManage))
        {
            return Error.Forbidden;
        }

        var now = time.GetUtcNow().UtcDateTime;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = await keys.InitializeAsync(db, now, ct);
        await audit.AppendAsync(db, "backup.recovery_key_created", "Backup", null, "Yeni kurtarma anahtarı oluşturuldu", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return key;
    }

    /// <summary>The user types the recovery key back once, proving it was written down correctly.</summary>
    public async Task<Result<bool>> ConfirmRecoveryKeyAsync(string typedKey, CancellationToken ct = default)
    {
        var parsed = RecoveryKey.Parse(typedKey);
        if (parsed.IsFailure)
        {
            return parsed.Error;
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var material = await keys.LoadAsync(db, ct);
        if (material is null)
        {
            return BackupErrors.NoKeyConfigured;
        }

        var kek = parsed.Value.DeriveKek(material.Value.Salt);
        var matches = CryptographicOperations.FixedTimeEquals(kek, material.Value.Kek);
        CryptographicOperations.ZeroMemory(kek);
        if (!matches)
        {
            return BackupErrors.WrongKey;
        }

        await SettingsStore.SetAsync(db, SettingKeys.BackupRecoveryKeyConfirmed, "true", time.GetUtcNow().UtcDateTime, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public static bool IsDue(BackupSchedule schedule, DateTime? lastSuccessUtc, DateTime nowUtc, TimeZoneInfo zone)
    {
        if (schedule == BackupSchedule.Off)
        {
            return false;
        }

        if (lastSuccessUtc is null)
        {
            return true;
        }

        var last = TimeZones.LocalDate(lastSuccessUtc.Value, zone);
        var today = TimeZones.LocalDate(nowUtc, zone);
        return schedule switch
        {
            BackupSchedule.Daily => today > last,
            BackupSchedule.Weekly => today.DayNumber - last.DayNumber >= 7,
            BackupSchedule.Monthly => today >= last.AddMonths(1),
            _ => false,
        };
    }

    public async Task<bool> IsScheduledBackupDueAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.KeyConfigured)
        {
            return false;
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.BusinessSettings.AnyAsync(ct))
        {
            return false;
        }

        var (_, zone) = await BusinessContext.LoadAsync(db, ct);
        return IsDue(status.Schedule, status.LastSuccessUtc, time.GetUtcNow().UtcDateTime, zone);
    }

    public async Task<Result<BackupRunResult>> CreateBackupAsync(BackupTrigger trigger, CancellationToken ct = default)
    {
        if (trigger == BackupTrigger.Manual && !session.Has(Permission.BackupsCreate))
        {
            return Error.Forbidden;
        }

        if (!await Gate.WaitAsync(TimeSpan.FromMinutes(5), ct))
        {
            return new Error("backup.busy", "Başka bir yedekleme işlemi sürüyor.");
        }

        try
        {
            return await CreateBackupCoreAsync(trigger, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<BackupListItem>> ListBackupsAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        var target = new LocalFolderBackupTarget(status.Folder);
        var result = new List<BackupListItem>();
        foreach (var file in await target.ListAsync(ct))
        {
            result.Add(await DescribeAsync(Path.Combine(target.Folder, file.Name), status.KeyId, ct));
        }

        return result;
    }

    public static async Task<BackupListItem> DescribeAsync(string path, string? currentKeyId, CancellationToken ct)
    {
        var info = new FileInfo(path);
        await using var fs = File.OpenRead(path);
        var header = await BackupCrypto.ReadHeaderAsync(fs, ct);
        return header.IsSuccess
            ? new BackupListItem(info.Name, header.Value.CreatedAtUtc, Enum.TryParse<BackupTrigger>(header.Value.Trigger, out var t) ? t : null, info.Length,
                header.Value.AppVersion, header.Value.SchemaVersion, header.Value.KeyId == currentKeyId, null)
            : new BackupListItem(info.Name, BackupNaming.TryParseCreatedAt(info.Name), BackupNaming.TryParseTrigger(info.Name), info.Length, null, null, false, header.Error.Message);
    }

    /// <summary>Full test restore into a temporary folder: decrypt, authenticate, check manifest hashes and SQLite integrity.</summary>
    public async Task<Result<BackupVerifyResult>> VerifyAsync(string backupPath, string? recoveryKey, CancellationToken ct = default)
    {
        var work = Path.Combine(env.TempDirectory, "verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            var opened = await OpenAsync(backupPath, recoveryKey, work, ct);
            return opened.IsFailure ? opened.Error : opened.Value.Result;
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    /// <summary>
    /// Replaces the live database with a verified backup. A safety copy of the current database is always taken first
    /// (plain local snapshot + encrypted pre-restore backup when keys are configured). The caller must sign out
    /// afterwards, because users and settings come from the restored data.
    /// </summary>
    public async Task<Result<RestoreResult>> RestoreAsync(string backupPath, string? recoveryKey, CancellationToken ct = default)
    {
        if (!session.Has(Permission.BackupsRestore))
        {
            return Error.Forbidden;
        }

        if (!await Gate.WaitAsync(TimeSpan.FromMinutes(5), ct))
        {
            return new Error("backup.busy", "Başka bir yedekleme işlemi sürüyor.");
        }

        var work = Path.Combine(env.TempDirectory, "restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            var opened = await OpenAsync(backupPath, recoveryKey, work, ct);
            if (opened.IsFailure)
            {
                return opened.Error;
            }

            var (verified, extractedDb, kek) = opened.Value;
            await using (var probe = await factory.CreateDbContextAsync(ct))
            {
                var known = probe.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
                var inBackup = await SqliteSetup.ReadAppliedMigrationsAsync(extractedDb, ct);
                if (inBackup.Any(m => !known.Contains(m)))
                {
                    return BackupErrors.UnsupportedVersion;
                }
            }

            // 1) Safety copies of the current data.
            Directory.CreateDirectory(env.SafetyDirectory);
            var safetyCopy = Path.Combine(env.SafetyDirectory, $"prerestore-{time.GetUtcNow().UtcDateTime:yyyyMMdd-HHmmss}.db");
            await SqliteSetup.SnapshotAsync(env.DatabasePath, safetyCopy, ct);
            string? safetyBackup = null;
            var pre = await CreateBackupCoreAsync(BackupTrigger.PreRestore, ct);
            if (pre.IsSuccess)
            {
                safetyBackup = pre.Value.FileName;
            }

            // 2) Swap files.
            SqliteSetup.ReleaseFileHandles();
            try
            {
                SqliteSetup.DeleteDatabaseFiles(env.DatabasePath);
                File.Copy(extractedDb, env.DatabasePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "Restore swap failed; putting the previous database back.");
                SqliteSetup.DeleteDatabaseFiles(env.DatabasePath);
                File.Copy(safetyCopy, env.DatabasePath);
                return new Error("backup.restore_io", "Veritabanı dosyası değiştirilemedi; mevcut verileriniz geri yüklendi. Uygulamayı yeniden başlatıp tekrar deneyin.");
            }

            // 3) Bring an older backup up to the current schema (with its own pre-migration snapshot).
            await initializer.InitializeAsync(env.DatabasePath, env.SafetyDirectory, ct);

            // 4) Re-protect the backup key for this Windows account and record the restore in the restored log.
            var now = time.GetUtcNow().UtcDateTime;
            await using (var db = await factory.CreateDbContextAsync(ct))
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (kek is not null && await SettingsStore.GetAsync(db, SettingKeys.BackupKeyId, ct) == KeyDerivation.KeyId(kek))
                {
                    await SettingsStore.SetAsync(db, SettingKeys.BackupProtectedKek, Convert.ToBase64String(protector.Protect(kek)), now, ct);
                }

                await audit.AppendAsync(db, "backup.restored", "Backup", verified.BackupId,
                    $"Yedekten geri yüklendi: {Path.GetFileName(backupPath)} (oluşturma {verified.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC)", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            if (kek is not null)
            {
                CryptographicOperations.ZeroMemory(kek);
            }

            return new RestoreResult(backupPath, verified.CreatedAtUtc, safetyCopy, safetyBackup);
        }
        finally
        {
            TryDeleteDirectory(work);
            Gate.Release();
        }
    }

    private async Task<Result<BackupRunResult>> CreateBackupCoreAsync(BackupTrigger trigger, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var now = time.GetUtcNow().UtcDateTime;
        var backupId = Ids.New();
        var work = Path.Combine(env.TempDirectory, "backup-" + backupId.ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            byte[] kek;
            byte[] salt;
            string folder;
            string? businessName;
            Guid installationId;
            string schema;
            RetentionPolicy retention;
            await using (var db = await factory.CreateDbContextAsync(ct))
            {
                var material = await keys.LoadAsync(db, ct);
                if (material is null)
                {
                    return BackupErrors.NoKeyConfigured;
                }

                (kek, salt) = material.Value;
                folder = await SettingsStore.GetAsync(db, SettingKeys.BackupFolder, ct) ?? env.DefaultBackupDirectory;
                businessName = await db.BusinessSettings.AsNoTracking().Select(s => s.BusinessName).FirstOrDefaultAsync(ct);
                installationId = Guid.TryParse(await SettingsStore.GetAsync(db, SettingKeys.InstallationId, ct), out var iid) ? iid : Guid.Empty;
                schema = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault() ?? "none";
                var r = await SettingsStore.GetAsync(db, SettingKeys.BackupRetention, ct);
                retention = r is null ? RetentionPolicy.Default : JsonSerializer.Deserialize<RetentionPolicy>(r) ?? RetentionPolicy.Default;
            }

            var snapshot = Path.Combine(work, BackupArchive.DatabaseEntryName);
            await SqliteSetup.SnapshotAsync(env.DatabasePath, snapshot, ct);
            var zip = Path.Combine(work, "inner.zip");
            await BackupArchive.WriteAsync(zip, new BackupManifestSeed(backupId, now, env.AppVersion, schema, businessName), [(BackupArchive.DatabaseEntryName, snapshot)], ct);

            var fileName = BackupNaming.FileName(now, trigger, backupId);
            var encrypted = Path.Combine(work, fileName);
            await using (var input = File.OpenRead(zip))
            await using (var output = new FileStream(encrypted, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await BackupCrypto.EncryptAsync(input, output, new BackupHeaderSeed(backupId, now, env.AppVersion, schema, installationId, trigger, salt), kek, ct);
            }

            CryptographicOperations.ZeroMemory(kek);
            var target = new LocalFolderBackupTarget(folder);
            target.CleanupPartialFiles();
            await target.UploadAsync(encrypted, fileName, null, ct);
            var size = new FileInfo(encrypted).Length;

            var deleted = 0;
            if (trigger is BackupTrigger.Manual or BackupTrigger.Scheduled or BackupTrigger.OnExit)
            {
                var records = (await target.ListAsync(ct))
                    .Select(f => (f.Name, Created: BackupNaming.TryParseCreatedAt(f.Name), Trigger: BackupNaming.TryParseTrigger(f.Name)))
                    .Where(x => x.Created is not null && x.Trigger is not null)
                    .Select(x => new BackupRecord(x.Name, x.Created!.Value, x.Trigger!.Value))
                    .ToList();
                await using var zdb = await factory.CreateDbContextAsync(ct);
                var zone = (await zdb.BusinessSettings.AnyAsync(ct)) ? (await BusinessContext.LoadAsync(zdb, ct)).Zone : TimeZoneInfo.Utc;
                foreach (var old in BackupRetention.SelectForDeletion(records, retention, zone))
                {
                    await target.DeleteAsync(old.Name, ct);
                    deleted++;
                }
            }

            await using (var db = await factory.CreateDbContextAsync(ct))
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await SettingsStore.SetUtcAsync(db, SettingKeys.BackupLastSuccessUtc, now, now, ct);
                await SettingsStore.SetAsync(db, SettingKeys.BackupLastFile, fileName, now, ct);
                await SettingsStore.SetAsync(db, SettingKeys.BackupLastError, string.Empty, now, ct);
                await audit.AppendAsync(db, "backup.created", "Backup", backupId, $"Şifreli yedek oluşturuldu ({trigger}, {size / 1024} KB, {deleted} eski yedek silindi)", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            return new BackupRunResult(fileName, size, sw.Elapsed, deleted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or Microsoft.Data.Sqlite.SqliteException)
        {
            logger.LogError(ex, "Backup failed ({Trigger}).", trigger);
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                await SettingsStore.SetAsync(db, SettingKeys.BackupLastError, $"{time.GetUtcNow().UtcDateTime:O} {ex.GetType().Name}", time.GetUtcNow().UtcDateTime, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // ignore: reporting the failure must not hide the original error
            }

            return new Error("backup.failed", $"Yedek oluşturulamadı: {(ex is UnauthorizedAccessException ? "klasöre yazma izni yok" : ex is IOException ? "disk veya klasöre erişilemiyor" : "beklenmeyen hata")}.");
        }
        finally
        {
            TryDeleteDirectory(work);
        }
    }

    private async Task<Result<(BackupVerifyResult Result, string ExtractedDb, byte[]? Kek)>> OpenAsync(string backupPath, string? recoveryKey, string work, CancellationToken ct)
    {
        if (!File.Exists(backupPath))
        {
            return Error.NotFound("Yedek dosyası");
        }

        Directory.CreateDirectory(work);
        RecoveryKey? parsedKey = null;
        if (!string.IsNullOrWhiteSpace(recoveryKey))
        {
            var p = RecoveryKey.Parse(recoveryKey);
            if (p.IsFailure)
            {
                return p.Error;
            }

            parsedKey = p.Value;
        }

        (byte[] Kek, byte[] Salt)? stored;
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            stored = await keys.LoadAsync(db, ct);
        }

        byte[]? usedKek = null;
        var zip = Path.Combine(work, "inner.zip");
        Result<BackupHeader> decrypted;
        await using (var input = File.OpenRead(backupPath))
        await using (var output = new FileStream(zip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            decrypted = await BackupCrypto.DecryptAsync(input, output, header =>
            {
                if (parsedKey is not null)
                {
                    usedKek = parsedKey.DeriveKek(Convert.FromBase64String(header.KdfSalt));
                }
                else if (stored is { } s && header.KeyId == KeyDerivation.KeyId(s.Kek))
                {
                    usedKek = s.Kek;
                }

                return usedKek;
            }, ct);
        }

        if (decrypted.IsFailure)
        {
            return parsedKey is null && decrypted.Error == BackupErrors.WrongKey
                ? new Error("backup.need_recovery_key", "Bu yedek farklı bir kurtarma anahtarıyla oluşturulmuş. Yedeği açmak için kurtarma anahtarını girin.")
                : decrypted.Error;
        }

        var extractDir = Path.Combine(work, "extracted");
        var manifest = await BackupArchive.ExtractAndVerifyAsync(zip, extractDir, ct);
        if (manifest.IsFailure)
        {
            return manifest.Error;
        }

        var dbFile = Path.Combine(extractDir, BackupArchive.DatabaseEntryName);
        if (!File.Exists(dbFile) || !await SqliteSetup.IntegrityCheckAsync(dbFile, ct))
        {
            return BackupErrors.Corrupted;
        }

        SqliteSetup.ReleaseFileHandles();
        var m = manifest.Value;
        return (new BackupVerifyResult(m.BackupId, m.CreatedAtUtc, m.AppVersion, m.SchemaVersion, new FileInfo(dbFile).Length, m.AttachmentsIncluded), dbFile, usedKek is null ? null : (byte[])usedKek.Clone());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                SqliteSetup.ReleaseFileHandles();
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temp leftovers are cleaned up at next start.
        }
    }
}
