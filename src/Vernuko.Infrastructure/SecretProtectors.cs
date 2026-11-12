using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Vernuko.Application.Common;

namespace Vernuko.Infrastructure;

/// <summary>Windows DPAPI, bound to the current Windows user account.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("vernuko/secret/v1");

    public byte[] Protect(byte[] plaintext) => ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedData) => ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
}

/// <summary>
/// DEVELOPMENT ONLY (non-Windows builds/CI): AES-GCM with a key file next to the data. This gives no protection
/// against someone who can read the data folder and is never used by the Windows application.
/// </summary>
public sealed class DevelopmentSecretProtector(string keyFilePath) : ISecretProtector
{
    public byte[] Protect(byte[] plaintext)
    {
        var key = LoadKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plaintext, cipher, tag);
        return [.. nonce, .. tag, .. cipher];
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        var key = LoadKey();
        var plain = new byte[protectedData.Length - 28];
        using var gcm = new AesGcm(key, 16);
        gcm.Decrypt(protectedData.AsSpan(0, 12), protectedData.AsSpan(28), protectedData.AsSpan(12, 16), plain);
        return plain;
    }

    private byte[] LoadKey()
    {
        if (!File.Exists(keyFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(keyFilePath)!);
            File.WriteAllBytes(keyFilePath, RandomNumberGenerator.GetBytes(32));
        }

        return File.ReadAllBytes(keyFilePath);
    }
}
