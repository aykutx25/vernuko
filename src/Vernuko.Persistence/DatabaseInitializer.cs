using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vernuko.Persistence;

public sealed class DatabaseTooNewException(IReadOnlyList<string> unknownMigrations)
    : Exception("Veritabanı Vernuko'nun daha yeni bir sürümüyle güncellenmiş. Lütfen uygulamayı güncelleyin; veriler değiştirilmedi.")
{
    public IReadOnlyList<string> UnknownMigrations { get; } = unknownMigrations;
}

public sealed class DatabaseMigrationFailedException(string safetyCopyPath, Exception inner)
    : Exception("Veritabanı güncellemesi başarısız oldu; veritabanı güncelleme öncesi haline geri döndürüldü.", inner)
{
    public string SafetyCopyPath { get; } = safetyCopyPath;
}

public sealed record DatabaseInitResult(bool Created, IReadOnlyList<string> AppliedMigrations, string? SafetyCopyPath, string SchemaVersion);

/// <summary>
/// Opens/creates the database and applies schema migrations safely:
/// refuses databases written by a newer app (downgrade protection), snapshots the database before migrating,
/// and rolls back to the snapshot if a migration fails.
/// </summary>
public sealed class DatabaseInitializer(IDbContextFactory<VernukoDbContext> factory, ILogger<DatabaseInitializer> logger)
{
    /// <summary>Test hook: runs right before migrations are applied (used to simulate failures).</summary>
    public Func<VernukoDbContext, Task>? BeforeMigrate { get; set; }

    public async Task<DatabaseInitResult> InitializeAsync(string databasePath, string safetyDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        var existed = File.Exists(databasePath) && new FileInfo(databasePath).Length > 0;

        await using var db = await factory.CreateDbContextAsync(ct);
        var known = db.Database.GetMigrations().ToList();
        var applied = existed ? (await db.Database.GetAppliedMigrationsAsync(ct)).ToList() : [];
        var unknown = applied.Except(known, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new DatabaseTooNewException(unknown);
        }

        var pending = known.Except(applied, StringComparer.Ordinal).ToList();
        string? safetyCopy = null;
        if (pending.Count > 0 && existed)
        {
            Directory.CreateDirectory(safetyDirectory);
            safetyCopy = Path.Combine(safetyDirectory, $"premigration-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{(applied.Count > 0 ? applied[^1] : "empty")}.db");
            await SqliteSetup.SnapshotAsync(databasePath, safetyCopy, ct);
            logger.LogInformation("Pre-migration snapshot created ({Count} pending migrations).", pending.Count);
        }

        if (pending.Count > 0)
        {
            try
            {
                if (BeforeMigrate is not null)
                {
                    await BeforeMigrate(db);
                }

                await db.Database.MigrateAsync(ct);
            }
            catch (Exception ex) when (safetyCopy is not null && ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Migration failed; restoring the pre-migration snapshot.");
                await db.Database.CloseConnectionAsync();
                SqliteSetup.ReleaseFileHandles();
                SqliteSetup.DeleteDatabaseFiles(databasePath);
                File.Copy(safetyCopy, databasePath);
                throw new DatabaseMigrationFailedException(safetyCopy, ex);
            }
        }

        await using (var connection = new SqliteConnection(SqliteSetup.ConnectionString(databasePath)))
        {
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return new DatabaseInitResult(!existed, pending, safetyCopy, known.Count > 0 ? known[^1] : "none");
    }
}
