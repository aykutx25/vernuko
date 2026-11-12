namespace Vernuko.Backups;

public sealed record RemoteBackupFile(string Name, long Size, DateTime ModifiedUtc);

public sealed record StorageQuota(long? TotalBytes, long? UsedBytes)
{
    public long? FreeBytes => TotalBytes is { } t && UsedBytes is { } u ? t - u : null;
}

/// <summary>
/// A place where encrypted .vbk files are stored. Implementations: local/USB folder (Phase 1);
/// Google Drive, OneDrive, Dropbox adapters are planned (Phase 4) and are NOT implemented yet.
/// Targets only ever receive already-encrypted files.
/// </summary>
public interface IBackupTarget
{
    string Kind { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<RemoteBackupFile>> ListAsync(CancellationToken ct);

    Task UploadAsync(string localFile, string name, IProgress<long>? progress, CancellationToken ct);

    Task DownloadAsync(string name, string localDestination, CancellationToken ct);

    Task DeleteAsync(string name, CancellationToken ct);

    Task<StorageQuota?> GetQuotaAsync(CancellationToken ct);
}

/// <summary>Local disk, second disk or USB drive folder.</summary>
public sealed class LocalFolderBackupTarget : IBackupTarget
{
    public const string Extension = ".vbk";
    private const string PartialSuffix = ".partial";

    public LocalFolderBackupTarget(string folder)
    {
        Folder = Path.GetFullPath(folder);
    }

    public string Folder { get; }

    public string Kind => "local";

    public string DisplayName => Folder;

    public Task<IReadOnlyList<RemoteBackupFile>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(Folder))
        {
            return Task.FromResult<IReadOnlyList<RemoteBackupFile>>([]);
        }

        IReadOnlyList<RemoteBackupFile> files = new DirectoryInfo(Folder)
            .EnumerateFiles("*" + Extension)
            .Select(f => new RemoteBackupFile(f.Name, f.Length, f.LastWriteTimeUtc))
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(files);
    }

    public async Task UploadAsync(string localFile, string name, IProgress<long>? progress, CancellationToken ct)
    {
        var target = SafePath(name);
        Directory.CreateDirectory(Folder);
        var partial = target + PartialSuffix;
        try
        {
            await using (var src = File.OpenRead(localFile))
            await using (var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                var buffer = new byte[81920];
                long total = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    total += n;
                    progress?.Report(total);
                }

                await dst.FlushAsync(ct);
            }

            File.Move(partial, target, overwrite: false);
        }
        finally
        {
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }
        }
    }

    public async Task DownloadAsync(string name, string localDestination, CancellationToken ct)
    {
        await using var src = File.OpenRead(SafePath(name));
        await using var dst = new FileStream(localDestination, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, ct);
    }

    public Task DeleteAsync(string name, CancellationToken ct)
    {
        var path = SafePath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<StorageQuota?> GetQuotaAsync(CancellationToken ct)
    {
        try
        {
            var root = Path.GetPathRoot(Folder);
            if (root is null)
            {
                return Task.FromResult<StorageQuota?>(null);
            }

            var drive = new DriveInfo(root);
            return Task.FromResult<StorageQuota?>(new StorageQuota(drive.TotalSize, drive.TotalSize - drive.AvailableFreeSpace));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult<StorageQuota?>(null);
        }
    }

    /// <summary>Removes leftovers of interrupted writes.</summary>
    public int CleanupPartialFiles()
    {
        if (!Directory.Exists(Folder))
        {
            return 0;
        }

        var count = 0;
        foreach (var f in Directory.EnumerateFiles(Folder, "*" + Extension + PartialSuffix))
        {
            File.Delete(f);
            count++;
        }

        return count;
    }

    private string SafePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("..", StringComparison.Ordinal)
            || !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid backup file name.", nameof(name));
        }

        return Path.Combine(Folder, name);
    }
}

public static class BackupNaming
{
    /// <summary>
    /// "vernuko-20261112-091530-manual-1a2b3c4d.vbk" — sortable, no personal data. The suffix uses the random tail of
    /// the (version 7) backup id; its leading characters are a timestamp and would collide within the same minute.
    /// </summary>
    public static string FileName(DateTime createdAtUtc, BackupTrigger trigger, Guid backupId) =>
        $"vernuko-{createdAtUtc:yyyyMMdd-HHmmss}-{trigger.ToString().ToLowerInvariant()}-{backupId.ToString("N")[^8..]}{LocalFolderBackupTarget.Extension}";

    public static BackupTrigger? TryParseTrigger(string fileName)
    {
        var parts = Path.GetFileNameWithoutExtension(fileName).Split('-');
        return parts.Length >= 5 && Enum.TryParse<BackupTrigger>(parts[3], ignoreCase: true, out var t) ? t : null;
    }

    public static DateTime? TryParseCreatedAt(string fileName)
    {
        var parts = Path.GetFileNameWithoutExtension(fileName).Split('-');
        return parts.Length >= 5 && DateTime.TryParseExact(parts[1] + parts[2], "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : null;
    }
}
