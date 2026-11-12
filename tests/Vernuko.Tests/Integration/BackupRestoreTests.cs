using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class BackupRestoreTests
{
    [Fact]
    public async Task Encrypted_backup_restores_previous_state()
    {
        await using var app = await TestApp.CreateAsync();
        var setup = await app.SetupBeautyAsync();
        Assert.NotNull(setup.RecoveryKey);
        var keep = await app.AddCustomerAsync("Yedekte", "Olan", "0532 111 11 11");
        var backups = app.Get<BackupService>();
        var run = await backups.CreateBackupAsync(BackupTrigger.Manual);
        Assert.True(run.IsSuccess, run.IsFailure ? run.Error.Message : null);

        // The file must not contain readable personal data.
        var path = Path.Combine(app.Root, "backups", run.Value.FileName);
        var raw = await File.ReadAllBytesAsync(path);
        Assert.DoesNotContain("Yedekte", System.Text.Encoding.UTF8.GetString(raw), StringComparison.Ordinal);
        Assert.DoesNotContain("SQLite format", System.Text.Encoding.ASCII.GetString(raw), StringComparison.Ordinal);

        // Changes after the backup...
        await app.AddCustomerAsync("Sonradan", "Eklenen", "0532 222 22 22");
        await app.Get<CustomerService>().AnonymizeAsync(keep, "test");

        var verify = await backups.VerifyAsync(path, null);
        Assert.True(verify.IsSuccess, verify.IsFailure ? verify.Error.Message : null);
        var restore = await backups.RestoreAsync(path, null);
        Assert.True(restore.IsSuccess, restore.IsFailure ? restore.Error.Message : null);
        Assert.True(File.Exists(restore.Value.SafetyCopyPath));
        Assert.NotNull(restore.Value.SafetyBackupFile);

        // ...are gone, the backed-up state is back, and the restore itself is audited.
        await app.Get<Vernuko.Application.Services.AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
        var list = (await app.Get<CustomerService>().SearchAsync(null)).Value;
        Assert.Contains(list, c => c.FullName == "Yedekte Olan");
        Assert.DoesNotContain(list, c => c.FullName == "Sonradan Eklenen");
        var audit = (await app.Get<AuditService>().GetRecentAsync(5)).Value;
        Assert.Contains(audit, e => e.Action == "backup.restored");
        Assert.True((await app.Get<AuditService>().VerifyChainAsync()).Value.IsIntact);
    }

    [Fact]
    public async Task Restore_on_a_new_computer_needs_the_recovery_key()
    {
        await using var original = await TestApp.CreateAsync();
        var key = (await original.SetupBeautyAsync()).RecoveryKey!;
        await original.AddCustomerAsync("Taşınan", "Müşteri", "0532 333 33 33");
        var run = (await original.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual)).Value;
        var backupFile = Path.Combine(original.Root, "backups", run.FileName);

        // New PC: different Windows account (DPAPI key), fresh installation.
        await using var fresh = await TestApp.CreateAsync(protector: new TestSecretProtector());
        await fresh.SetupBeautyAsync();
        var noKey = await fresh.Get<BackupService>().RestoreAsync(backupFile, null);
        Assert.Equal("backup.need_recovery_key", noKey.Error.Code);

        var wrongKey = await fresh.Get<BackupService>().RestoreAsync(backupFile, RecoveryKey.Generate().ToString());
        Assert.Equal(BackupErrors.WrongKey, wrongKey.Error);
        Assert.Equal(BackupErrors.RecoveryKeyFormat, (await fresh.Get<BackupService>().RestoreAsync(backupFile, "yanlış biçim")).Error);

        var ok = await fresh.Get<BackupService>().RestoreAsync(backupFile, key);
        Assert.True(ok.IsSuccess, ok.IsFailure ? ok.Error.Message : null);
        await fresh.Get<Vernuko.Application.Services.AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
        Assert.Single((await fresh.Get<CustomerService>().SearchAsync("taşınan")).Value);

        // The backup key was re-protected for this account: new backups work without typing the key again.
        var again = await fresh.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual);
        Assert.True(again.IsSuccess, again.IsFailure ? again.Error.ToString() : null);
    }

    [Fact]
    public async Task Corrupted_backup_is_rejected_and_live_data_untouched()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        await app.AddCustomerAsync();
        var run = (await app.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual)).Value;
        var path = Path.Combine(app.Root, "backups", run.FileName);
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[^50] ^= 0xFF;
        var corrupted = Path.Combine(app.Root, "corrupted.vbk");
        await File.WriteAllBytesAsync(corrupted, bytes);

        Assert.Equal(BackupErrors.Corrupted, (await app.Get<BackupService>().VerifyAsync(corrupted, null)).Error);
        Assert.Equal(BackupErrors.Corrupted, (await app.Get<BackupService>().RestoreAsync(corrupted, null)).Error);
        var truncated = Path.Combine(app.Root, "truncated.vbk");
        await File.WriteAllBytesAsync(truncated, bytes[..(bytes.Length / 2)]);
        Assert.Equal(BackupErrors.Corrupted, (await app.Get<BackupService>().VerifyAsync(truncated, null)).Error);
        Assert.Single((await app.Get<CustomerService>().SearchAsync(null)).Value);
    }

    [Fact]
    public async Task Recovery_key_confirmation_and_listing()
    {
        await using var app = await TestApp.CreateAsync();
        var key = (await app.SetupBeautyAsync()).RecoveryKey!;
        var svc = app.Get<BackupService>();
        Assert.False((await svc.GetStatusAsync()).RecoveryKeyConfirmed);
        Assert.True((await svc.ConfirmRecoveryKeyAsync(RecoveryKey.Generate().ToString())).IsFailure);
        Assert.True((await svc.ConfirmRecoveryKeyAsync(key.ToLowerInvariant())).IsSuccess);
        Assert.True((await svc.GetStatusAsync()).RecoveryKeyConfirmed);

        Assert.True(await svc.IsScheduledBackupDueAsync());
        await svc.CreateBackupAsync(BackupTrigger.Scheduled);
        Assert.False(await svc.IsScheduledBackupDueAsync());
        app.Time.Advance(TimeSpan.FromDays(1));
        Assert.True(await svc.IsScheduledBackupDueAsync());

        var listed = await svc.ListBackupsAsync();
        Assert.Single(listed);
        Assert.True(listed[0].KeyMatches);
        Assert.Equal(BackupTrigger.Scheduled, listed[0].Trigger);
    }

    [Fact]
    public async Task Backup_without_configured_key_reports_clearly()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync(enableBackup: false);
        Assert.Equal(BackupErrors.NoKeyConfigured, (await app.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual)).Error);
        var key = await app.Get<BackupService>().CreateRecoveryKeyAsync();
        Assert.True(key.IsSuccess);
        Assert.True((await app.Get<BackupService>().CreateBackupAsync(BackupTrigger.Manual)).IsSuccess);
    }
}
