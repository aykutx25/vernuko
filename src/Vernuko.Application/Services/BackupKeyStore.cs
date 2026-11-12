using System.Security.Cryptography;
using Vernuko.Application.Common;
using Vernuko.Backups;
using Vernuko.Persistence;

namespace Vernuko.Application.Services;

/// <summary>
/// Stores the backup key-encryption key (derived from the recovery key) protected by the OS secret store.
/// The recovery key itself is never stored.
/// </summary>
public sealed class BackupKeyStore(ISecretProtector protector, AttachmentKeyStore attachmentKeys)
{
    public async Task<string> InitializeAsync(VernukoDbContext db, DateTime nowUtc, CancellationToken ct)
    {
        var recovery = RecoveryKey.Generate();
        var salt = RandomNumberGenerator.GetBytes(16);
        var kek = recovery.DeriveKek(salt);
        await SettingsStore.SetAsync(db, SettingKeys.BackupKdfSalt, Convert.ToBase64String(salt), nowUtc, ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupProtectedKek, Convert.ToBase64String(protector.Protect(kek)), nowUtc, ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupKeyId, KeyDerivation.KeyId(kek), nowUtc, ct);
        await SettingsStore.SetAsync(db, SettingKeys.BackupRecoveryKeyConfirmed, "false", nowUtc, ct);
        await attachmentKeys.RewrapAsync(db, kek, nowUtc, ct);
        CryptographicOperations.ZeroMemory(kek);
        return recovery.ToString();
    }

    public async Task<(byte[] Kek, byte[] Salt)?> LoadAsync(VernukoDbContext db, CancellationToken ct)
    {
        var salt = await SettingsStore.GetAsync(db, SettingKeys.BackupKdfSalt, ct);
        var protectedKek = await SettingsStore.GetAsync(db, SettingKeys.BackupProtectedKek, ct);
        if (salt is null || protectedKek is null)
        {
            return null;
        }

        try
        {
            return (protector.Unprotect(Convert.FromBase64String(protectedKek)), Convert.FromBase64String(salt));
        }
        catch (CryptographicException)
        {
            // e.g. database restored on another Windows account/PC: DPAPI cannot unprotect; the recovery key is needed.
            return null;
        }
    }
}
