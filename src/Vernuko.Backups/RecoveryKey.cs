using System.Security.Cryptography;
using System.Text;
using Vernuko.Core;

namespace Vernuko.Backups;

/// <summary>
/// The business' recovery key: 160 random bits shown once as 8 groups of 4 Base32 characters
/// (e.g. "K7QF-2M9X-..."). It is the only way to open backups on a new computer, so it must be written down
/// and kept away from the PC. The application stores only the derived key, protected by Windows DPAPI.
/// </summary>
public sealed class RecoveryKey
{
    public const int KeyBytes = 20;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I, O, 0, 1 (easy to misread)

    private readonly byte[] _bytes;

    private RecoveryKey(byte[] bytes)
    {
        _bytes = bytes;
    }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public static RecoveryKey Generate() => new(RandomNumberGenerator.GetBytes(KeyBytes));

    public static Result<RecoveryKey> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BackupErrors.RecoveryKeyFormat;
        }

        var cleaned = new StringBuilder();
        foreach (var ch in text.ToUpperInvariant())
        {
            if (ch is '-' or ' ')
            {
                continue;
            }

            // Forgive the most common misreadings.
            var c = ch switch { 'O' => 'Q', '0' => 'Q', 'I' => 'J', '1' => 'J', _ => ch };
            if (!Alphabet.Contains(c, StringComparison.Ordinal))
            {
                return BackupErrors.RecoveryKeyFormat;
            }

            cleaned.Append(c);
        }

        if (cleaned.Length != KeyBytes * 8 / 5)
        {
            return BackupErrors.RecoveryKeyFormat;
        }

        var bytes = new byte[KeyBytes];
        var buffer = 0;
        var bits = 0;
        var index = 0;
        foreach (var c in cleaned.ToString())
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
                buffer &= (1 << bits) - 1;
            }
        }

        return new RecoveryKey(bytes);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        var buffer = 0;
        var bits = 0;
        foreach (var b in _bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }

        var raw = sb.ToString();
        return string.Join('-', Enumerable.Range(0, raw.Length / 4).Select(i => raw.Substring(i * 4, 4)));
    }

    /// <summary>Key-encryption key used to wrap per-backup file keys.</summary>
    public byte[] DeriveKek(byte[] salt) => KeyDerivation.DeriveKek(_bytes, salt);
}

public static class KeyDerivation
{
    private static readonly byte[] KekInfo = Encoding.ASCII.GetBytes("vernuko/backup/kek/v1");

    /// <summary>HKDF is appropriate because the input is a uniformly random 160-bit key, not a human password.</summary>
    public static byte[] DeriveKek(ReadOnlySpan<byte> recoveryKey, byte[] salt) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, recoveryKey.ToArray(), 32, salt, KekInfo);

    /// <summary>Short public identifier of a KEK so the UI can tell which recovery key a backup needs.</summary>
    public static string KeyId(byte[] kek) =>
        Convert.ToHexStringLower(SHA256.HashData([.. Encoding.ASCII.GetBytes("vernuko/backup/keyid/v1"), .. kek]))[..16];
}
