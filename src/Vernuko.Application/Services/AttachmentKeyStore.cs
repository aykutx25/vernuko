using System.Security.Cryptography;
using System.Text;
using Vernuko.Application.Common;
using Vernuko.Backups;
using Vernuko.Core;
using Vernuko.Persistence;

namespace Vernuko.Application.Services;

/// <summary>
/// Key for encrypting customer attachments at rest. Stored twice: protected by the OS (DPAPI) for daily use, and
/// wrapped with the backup key-encryption key so that a restore with the recovery key on a new computer can also
/// open the attachments.
/// </summary>
public sealed class AttachmentKeyStore(ISecretProtector protector)
{
    public const string ProtectedKeySetting = "attachments.protected_key";
    public const string WrappedKeySetting = "attachments.key_wrapped_by_kek";

    public static readonly Error KeyUnavailable = new("attachments.key_unavailable",
        "Dosya ve not şifreleme anahtarı bu Windows hesabında açılamıyor (parola sıfırlama veya veri klasörü başka hesaba taşınmış olabilir). " +
        "Mevcut kayıtlarınız korunuyor; erişimi geri almak için kurtarma anahtarıyla yedekten geri yükleyin.");
    private static readonly byte[] WrapAad = Encoding.ASCII.GetBytes("vernuko/attachments/key/v1");

    /// <summary>
    /// The attachment key, created on first use. Null when a key exists but this Windows account cannot open it
    /// (password reset, data folder copied to another account): a new key must never replace it, or every earlier
    /// note and file becomes unreadable. Access comes back with a restore using the recovery key.
    /// </summary>
    public async Task<byte[]?> GetOrCreateAsync(VernukoDbContext db, DateTime nowUtc, CancellationToken ct)
    {
        var existing = await TryLoadAsync(db, ct);
        if (existing is not null)
        {
            return existing;
        }

        if (await SettingsStore.GetAsync(db, ProtectedKeySetting, ct) is not null)
        {
            return null;
        }

        var key = RandomNumberGenerator.GetBytes(32);
        await SettingsStore.SetAsync(db, ProtectedKeySetting, Convert.ToBase64String(protector.Protect(key)), nowUtc, ct);
        if (await TryLoadBackupKekAsync(db, ct) is { } kek)
        {
            await SettingsStore.SetAsync(db, WrappedKeySetting, Wrap(key, kek), nowUtc, ct);
            CryptographicOperations.ZeroMemory(kek);
        }

        return key;
    }

    public async Task<byte[]?> TryLoadAsync(VernukoDbContext db, CancellationToken ct)
    {
        var stored = await SettingsStore.GetAsync(db, ProtectedKeySetting, ct);
        if (stored is null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(Convert.FromBase64String(stored));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Called when a new backup recovery key is created: re-wraps the attachment key for it.</summary>
    public async Task RewrapAsync(VernukoDbContext db, byte[] newKek, DateTime nowUtc, CancellationToken ct)
    {
        if (await TryLoadAsync(db, ct) is { } key)
        {
            await SettingsStore.SetAsync(db, WrappedKeySetting, Wrap(key, newKek), nowUtc, ct);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>After a restore with the recovery key: recover the attachment key and protect it for this Windows account.</summary>
    public async Task<bool> RecoverWithKekAsync(VernukoDbContext db, byte[] kek, DateTime nowUtc, CancellationToken ct)
    {
        var wrapped = await SettingsStore.GetAsync(db, WrappedKeySetting, ct);
        if (wrapped is null)
        {
            return false;
        }

        var key = Unwrap(wrapped, kek);
        if (key is null)
        {
            return false;
        }

        await SettingsStore.SetAsync(db, ProtectedKeySetting, Convert.ToBase64String(protector.Protect(key)), nowUtc, ct);
        CryptographicOperations.ZeroMemory(key);
        return true;
    }

    private async Task<byte[]?> TryLoadBackupKekAsync(VernukoDbContext db, CancellationToken ct)
    {
        var protectedKek = await SettingsStore.GetAsync(db, SettingKeys.BackupProtectedKek, ct);
        if (protectedKek is null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(Convert.FromBase64String(protectedKek));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string Wrap(byte[] key, byte[] kek)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[key.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(kek, 16);
        gcm.Encrypt(nonce, key, cipher, tag, WrapAad);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    private static byte[]? Unwrap(string wrapped, byte[] kek)
    {
        try
        {
            var data = Convert.FromBase64String(wrapped);
            var key = new byte[data.Length - 28];
            using var gcm = new AesGcm(kek, 16);
            gcm.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), key, WrapAad);
            return key;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>Single-shot AES-256-GCM for attachments (max 25 MB). Layout: magic ‖ nonce ‖ tag ‖ ciphertext; AAD = file id.</summary>
public static class AttachmentCrypto
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("VRNKAT01");

    public static byte[] Encrypt(byte[] key, Guid fileId, ReadOnlySpan<byte> plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plain.Length];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plain, cipher, tag, fileId.ToByteArray());
        return [.. Magic, .. nonce, .. tag, .. cipher];
    }

    public static byte[]? Decrypt(byte[] key, Guid fileId, byte[] data)
    {
        if (data.Length < Magic.Length + 28 || !data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            return null;
        }

        var offset = Magic.Length;
        var plain = new byte[data.Length - offset - 28];
        try
        {
            using var gcm = new AesGcm(key, 16);
            gcm.Decrypt(data.AsSpan(offset, 12), data.AsSpan(offset + 28), data.AsSpan(offset + 12, 16), plain, fileId.ToByteArray());
            return plain;
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }
    }
}
