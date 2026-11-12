using System.Text.Json.Serialization;

namespace Vernuko.Backups;

/// <summary>
/// Plain-text header of a .vbk file. It deliberately contains NO personal data: it is readable without the key
/// (to list backups) but every byte of it is authenticated as AAD of each encrypted chunk.
/// </summary>
public sealed record BackupHeader
{
    public const string Magic = "VRNKBK01";
    public const int CurrentFormatVersion = 1;
    public const int DefaultChunkSize = 1024 * 1024;

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    [JsonPropertyName("backupId")]
    public required Guid BackupId { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public required DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("appVersion")]
    public required string AppVersion { get; init; }

    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("installationId")]
    public required Guid InstallationId { get; init; }

    [JsonPropertyName("trigger")]
    public required string Trigger { get; init; }

    [JsonPropertyName("keyId")]
    public required string KeyId { get; init; }

    [JsonPropertyName("kdfSalt")]
    public required string KdfSalt { get; init; }

    [JsonPropertyName("wrapNonce")]
    public required string WrapNonce { get; init; }

    [JsonPropertyName("wrappedKey")]
    public required string WrappedKey { get; init; }

    [JsonPropertyName("noncePrefix")]
    public required string NoncePrefix { get; init; }

    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; init; } = DefaultChunkSize;

    [JsonPropertyName("cipher")]
    public string Cipher { get; init; } = "AES-256-GCM-STREAM";
}

/// <summary>Encrypted manifest stored inside the archive.</summary>
public sealed record BackupManifest
{
    [JsonPropertyName("backupId")]
    public required Guid BackupId { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public required DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("appVersion")]
    public required string AppVersion { get; init; }

    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("businessName")]
    public string? BusinessName { get; init; }

    [JsonPropertyName("files")]
    public required IReadOnlyList<BackupFileEntry> Files { get; init; }

    /// <summary>Phase 1 backups contain the database only. Photos/attachments will be listed here when supported.</summary>
    [JsonPropertyName("attachmentsIncluded")]
    public bool AttachmentsIncluded { get; init; }
}

public sealed record BackupFileEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256);

public enum BackupTrigger
{
    Manual,
    Scheduled,
    OnExit,
    PreMigration,
    PreRestore,
    PreUpdate,
}
