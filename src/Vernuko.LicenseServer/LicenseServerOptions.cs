using System.Security.Cryptography;
using System.Text;

namespace Vernuko.LicenseServer;

/// <summary>
/// Server settings (appsettings / environment variables, section "LicenseServer"). Secrets are given as FILE
/// paths readable only by the service user — never written into the repository or appsettings.json.
/// </summary>
public sealed class LicenseServerOptions
{
    public string ConnectionString { get; set; } = "Data Source=license.db";

    /// <summary>PEM file with the ECDSA P-256 private signing key (created with <c>generate-key</c>).</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Key id written into every license; the desktop app looks up the matching public key by it.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>File with the secret used to derive license keys from license ids (32+ random bytes, base64).</summary>
    public string KeySecretPath { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) of the admin API key. The key itself is not stored on the server.</summary>
    public string AdminKeySha256 { get; set; } = string.Empty;

    /// <summary>File with the Paddle notification destination secret (pdl_ntfset_…). Empty = Paddle webhooks off.</summary>
    public string PaddleWebhookSecretPath { get; set; } = string.Empty;

    /// <summary>Allowed difference between Paddle's signature timestamp and our clock.</summary>
    public int PaddleTimestampToleranceSeconds { get; set; } = 300;

    /// <summary>Paddle price id → what the customer bought.</summary>
    public Dictionary<string, PlanDefinition> PaddlePrices { get; set; } = [];

    /// <summary>Folder with the Velopack release files served to licensed installations. Empty = updates off.</summary>
    public string ReleasesPath { get; set; } = string.Empty;

    /// <summary>The app checks in online about this often (rfr in the license document).</summary>
    public int RefreshDays { get; set; } = 7;

    /// <summary>Extra days after the paid period, so a slightly late renewal never locks anyone.</summary>
    public int PaymentToleranceDays { get; set; } = 7;

    /// <summary>Offline days allowed after the refresh time (grace in the license document).</summary>
    public int OfflineGraceDays { get; set; } = 14;

    public int TrialDays { get; set; } = 30;
}

public sealed class PlanDefinition
{
    public string Edition { get; set; } = "Standart";

    public int MaxDevices { get; set; } = 1;

    public List<string> Features { get; set; } = [];
}

/// <summary>Secrets loaded once at start-up from the configured files.</summary>
public sealed class ServerSecrets : IDisposable
{
    private ServerSecrets(ECDsa signingKey, string keyId, byte[] keySecret, byte[] adminKeyHash, byte[]? paddleSecret)
    {
        SigningKey = signingKey;
        KeyId = keyId;
        KeySecret = keySecret;
        AdminKeyHash = adminKeyHash;
        PaddleSecret = paddleSecret;
    }

    public ECDsa SigningKey { get; }

    public string KeyId { get; }

    public byte[] KeySecret { get; }

    public byte[] AdminKeyHash { get; }

    public byte[]? PaddleSecret { get; }

    public static ServerSecrets Load(LicenseServerOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.SigningKeyPath) || string.IsNullOrWhiteSpace(o.SigningKeyId))
        {
            throw new InvalidOperationException("LicenseServer:SigningKeyPath and LicenseServer:SigningKeyId are required.");
        }

        var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(o.SigningKeyPath));
        if (key.KeySize != 256)
        {
            throw new InvalidOperationException("The signing key must be ECDSA P-256.");
        }

        var secret = Convert.FromBase64String(File.ReadAllText(o.KeySecretPath).Trim());
        if (secret.Length < 32)
        {
            throw new InvalidOperationException("The key secret must be at least 32 bytes.");
        }

        var admin = Convert.FromHexString(o.AdminKeySha256);
        if (admin.Length != 32)
        {
            throw new InvalidOperationException("LicenseServer:AdminKeySha256 must be a SHA-256 hex string.");
        }

        byte[]? paddle = string.IsNullOrWhiteSpace(o.PaddleWebhookSecretPath) ? null : Encoding.UTF8.GetBytes(File.ReadAllText(o.PaddleWebhookSecretPath).Trim());
        return new ServerSecrets(key, o.SigningKeyId, secret, admin, paddle);
    }

    /// <summary>Test helper.</summary>
    public static ServerSecrets Create(ECDsa signingKey, string keyId, byte[] keySecret, string adminKey, string? paddleSecret) =>
        new(signingKey, keyId, keySecret, SHA256.HashData(Encoding.UTF8.GetBytes(adminKey)), paddleSecret is null ? null : Encoding.UTF8.GetBytes(paddleSecret));

    public bool IsAdmin(string? presentedKey) =>
        presentedKey is { Length: > 0 } && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey)), AdminKeyHash);

    public void Dispose() => SigningKey.Dispose();
}

/// <summary>
/// License keys: "VRNK-XXXXX-XXXXX-XXXXX-XXXXX" (Crockford base32, 100 bits), derived as HMAC(secret, license id)
/// so support can re-send a key without the server storing it. Lookup uses SHA-256 of the normalized key.
/// </summary>
public static class LicenseKeys
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewLicenseId() => "lic_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(10));

    public static string NewActivationId() => "act_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(10));

    public static string Derive(byte[] secret, string licenseId)
    {
        var mac = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(licenseId));
        var chars = new char[20];
        ulong acc = 0;
        var bits = 0;
        var pos = 0;
        var index = 0;
        while (pos < chars.Length)
        {
            if (bits < 5)
            {
                acc = (acc << 8) | mac[index++];
                bits += 8;
            }

            chars[pos++] = Alphabet[(int)((acc >> (bits - 5)) & 31)];
            bits -= 5;
        }

        var s = new string(chars);
        return $"VRNK-{s[..5]}-{s[5..10]}-{s[10..15]}-{s[15..20]}";
    }

    /// <summary>Upper-case, no spaces or dashes, common look-alikes mapped (O→0, I/L→1, U→V).</summary>
    public static string? Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var ch in key.Trim().ToUpperInvariant())
        {
            var c = ch switch { 'O' => '0', 'I' or 'L' => '1', 'U' => 'V', _ => ch };
            if (c is '-' or ' ')
            {
                continue;
            }

            sb.Append(c);
        }

        var s = sb.ToString();
        if (s.Length == 24 && s.StartsWith("VRNK", StringComparison.Ordinal))
        {
            s = s[4..];
        }

        return s.Length == 20 && s.All(Alphabet.Contains) ? s : null;
    }

    public static string Hash(string normalized) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)));
}
