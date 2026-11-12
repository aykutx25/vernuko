using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Vernuko.Core;

namespace Vernuko.Backups;

/// <summary>Builds and opens the (unencrypted) inner ZIP: manifest.json + database snapshot.</summary>
public static class BackupArchive
{
    public const string ManifestName = "manifest.json";
    public const string DatabaseEntryName = "vernuko.db";

    public static async Task WriteAsync(string zipPath, BackupManifestSeed seed, IReadOnlyList<(string EntryName, string SourcePath)> files, CancellationToken ct)
    {
        var entries = new List<BackupFileEntry>();
        foreach (var (name, source) in files)
        {
            ValidateEntryName(name);
            await using var fs = File.OpenRead(source);
            var hash = await SHA256.HashDataAsync(fs, ct);
            entries.Add(new BackupFileEntry(name, fs.Length, Convert.ToHexStringLower(hash)));
        }

        var manifest = new BackupManifest
        {
            BackupId = seed.BackupId,
            CreatedAtUtc = seed.CreatedAtUtc,
            AppVersion = seed.AppVersion,
            SchemaVersion = seed.SchemaVersion,
            BusinessName = seed.BusinessName,
            Files = entries,
            AttachmentsIncluded = false,
        };

        await using var zipStream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);
        var manifestEntry = zip.CreateEntry(ManifestName, CompressionLevel.Optimal);
        await using (var ms = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(ms, manifest, cancellationToken: ct);
        }

        foreach (var (name, source) in files)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            await using var es = entry.Open();
            await using var fs = File.OpenRead(source);
            await fs.CopyToAsync(es, ct);
        }
    }

    /// <summary>
    /// Extracts the archive into <paramref name="destinationDirectory"/> and verifies every file against the manifest.
    /// Entry names are validated to prevent path traversal ("zip slip").
    /// </summary>
    public static async Task<Result<BackupManifest>> ExtractAndVerifyAsync(string zipPath, string destinationDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(destinationDirectory);
        var root = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var manifestEntry = zip.GetEntry(ManifestName);
            if (manifestEntry is null)
            {
                return BackupErrors.ManifestMismatch;
            }

            BackupManifest? manifest;
            await using (var ms = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(ms, cancellationToken: ct);
            }

            if (manifest is null || manifest.Files.Count == 0)
            {
                return BackupErrors.ManifestMismatch;
            }

            foreach (var file in manifest.Files)
            {
                if (!IsSafeEntryName(file.Name))
                {
                    return BackupErrors.ManifestMismatch;
                }

                var entry = zip.GetEntry(file.Name);
                if (entry is null)
                {
                    return BackupErrors.ManifestMismatch;
                }

                var target = Path.GetFullPath(Path.Combine(destinationDirectory, file.Name));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                {
                    return BackupErrors.ManifestMismatch;
                }

                await using (var es = entry.Open())
                await using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await es.CopyToAsync(fs, ct);
                }

                await using var verify = File.OpenRead(target);
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(verify, ct));
                if (verify.Length != file.Size || !string.Equals(hash, file.Sha256, StringComparison.Ordinal))
                {
                    return BackupErrors.ManifestMismatch;
                }
            }

            return manifest;
        }
        catch (InvalidDataException)
        {
            return BackupErrors.Corrupted;
        }
        catch (JsonException)
        {
            return BackupErrors.ManifestMismatch;
        }
    }

    private static void ValidateEntryName(string name)
    {
        if (!IsSafeEntryName(name))
        {
            throw new ArgumentException($"Unsafe archive entry name '{name}'.", nameof(name));
        }
    }

    private static bool IsSafeEntryName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 200 && !name.Contains("..", StringComparison.Ordinal)
        && !name.Contains('/', StringComparison.Ordinal) && !name.Contains('\\', StringComparison.Ordinal)
        && !name.Contains(':', StringComparison.Ordinal) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}

public sealed record BackupManifestSeed(Guid BackupId, DateTime CreatedAtUtc, string AppVersion, string SchemaVersion, string? BusinessName);
