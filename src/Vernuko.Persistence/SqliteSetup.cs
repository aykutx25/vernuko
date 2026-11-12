using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Vernuko.Persistence;

public static class SqliteSetup
{
    public static string ConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Private,
        Pooling = true,
        DefaultTimeout = 15,
    }.ToString();

    public static void Configure(DbContextOptionsBuilder options, string databasePath)
    {
        options.UseSqlite(ConnectionString(databasePath), o => o.CommandTimeout(30));
        options.AddInterceptors(PragmaInterceptor.Instance);
    }

    /// <summary>
    /// Creates a transactionally consistent copy of a live database (works while the app is running, WAL included).
    /// </summary>
    public static async Task SnapshotAsync(string sourceDatabasePath, string targetPath, CancellationToken ct)
    {
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        await using var connection = new SqliteConnection(ConnectionString(sourceDatabasePath));
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "VACUUM INTO $path;";
        cmd.Parameters.AddWithValue("$path", targetPath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Runs PRAGMA integrity_check on a database file (read-only).</summary>
    public static async Task<bool> IntegrityCheckAsync(string databasePath, CancellationToken ct)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        await using var connection = new SqliteConnection(cs);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = await cmd.ExecuteScalarAsync(ct) as string;
        return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Migration ids recorded inside a database file (empty if it was never migrated).</summary>
    public static async Task<IReadOnlyList<string>> ReadAppliedMigrationsAsync(string databasePath, CancellationToken ct)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        await using var connection = new SqliteConnection(cs);
        await connection.OpenAsync(ct);
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
        if (Convert.ToInt64(await check.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            return [];
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    /// <summary>Releases pooled connections so database files can be replaced or deleted.</summary>
    public static void ReleaseFileHandles() => SqliteConnection.ClearAllPools();

    public static void DeleteDatabaseFiles(string databasePath)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            var f = databasePath + suffix;
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
    }
}

/// <summary>Per-connection pragmas: enforce FKs and full durability (financial records must survive power loss).</summary>
public sealed class PragmaInterceptor : DbConnectionInterceptor
{
    public static readonly PragmaInterceptor Instance = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Apply(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection);
        return Task.CompletedTask;
    }

    private static void Apply(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL;";
        cmd.ExecuteNonQuery();
    }
}

/// <summary>Used only by "dotnet ef" at design time.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VernukoDbContext>
{
    public VernukoDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<VernukoDbContext>();
        SqliteSetup.Configure(builder, Path.Combine(Path.GetTempPath(), "vernuko-design.db"));
        return new VernukoDbContext(builder.Options, TimeProvider.System);
    }
}
