using System.Text.Json.Serialization;

namespace Vernuko.Licensing;

/// <summary>
/// Signed license payload issued by the Vernuko license server. The desktop app only holds PUBLIC keys;
/// the private signing key never ships with the application.
/// </summary>
public sealed record LicenseDocument
{
    [JsonPropertyName("v")]
    public int FormatVersion { get; init; } = 1;

    [JsonPropertyName("lid")]
    public required string LicenseId { get; init; }

    [JsonPropertyName("prd")]
    public required string Product { get; init; }

    [JsonPropertyName("ed")]
    public required string Edition { get; init; }

    [JsonPropertyName("feat")]
    public IReadOnlyList<string> Features { get; init; } = [];

    [JsonPropertyName("aid")]
    public required string ActivationId { get; init; }

    /// <summary>Salted hash of the machine identifier (never the raw hardware id).</summary>
    [JsonPropertyName("dev")]
    public required string DeviceHash { get; init; }

    [JsonPropertyName("max")]
    public int MaxDevices { get; init; } = 1;

    [JsonPropertyName("iat")]
    public required DateTime IssuedAtUtc { get; init; }

    /// <summary>End of the paid subscription period.</summary>
    [JsonPropertyName("exp")]
    public required DateTime ValidUntilUtc { get; init; }

    /// <summary>The app should refresh online before this time; after it, the offline grace window applies.</summary>
    [JsonPropertyName("rfr")]
    public required DateTime RefreshAfterUtc { get; init; }

    /// <summary>How long the license keeps working without contacting the server after <see cref="RefreshAfterUtc"/>.</summary>
    [JsonPropertyName("grace")]
    public int OfflineGraceDays { get; init; } = 14;
}

/// <summary>Wire format: base64url(payload JSON) + signature over those exact bytes + key id.</summary>
public sealed record SignedLicense(
    [property: JsonPropertyName("kid")] string KeyId,
    [property: JsonPropertyName("payload")] string Payload,
    [property: JsonPropertyName("sig")] string Signature,
    [property: JsonPropertyName("alg")] string Algorithm = "ES256");
