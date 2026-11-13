using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.Licensing;
using Vernuko.Persistence;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Regression tests for the infrastructure defects found in the general bug review (Kasım 2026).</summary>
public class InfraFixesTests
{
    [Fact]
    public void Backup_clean_up_only_claims_files_of_this_installation()
    {
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var created = new DateTime(2026, 11, 12, 9, 15, 30, DateTimeKind.Utc);
        var name = BackupNaming.FileName(created, BackupTrigger.Scheduled, Guid.NewGuid(), mine);
        Assert.True(BackupNaming.BelongsTo(name, mine));
        Assert.False(BackupNaming.BelongsTo(name, other));
        Assert.Equal(created, BackupNaming.TryParseCreatedAt(name));
        Assert.Equal(BackupTrigger.Scheduled, BackupNaming.TryParseTrigger(name));

        // Files from before the tag (or another branch) are never deleted automatically.
        Assert.False(BackupNaming.BelongsTo("vernuko-20261112-091530-manual-1a2b3c4d.vbk", mine));
    }

    [Fact]
    public async Task Clean_up_in_a_shared_folder_leaves_the_other_branch_alone()
    {
        var shared = Path.Combine(Path.GetTempPath(), "vernuko-tests", "shared-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shared);
        try
        {
            // The other branch's only backup, older than anything the retention policy keeps.
            var foreign = BackupNaming.FileName(new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc), BackupTrigger.Scheduled, Guid.NewGuid(), Guid.NewGuid());
            await File.WriteAllBytesAsync(Path.Combine(shared, foreign), [1, 2, 3]);

            await using var app = await TestApp.CreateAsync();
            await app.SetupBeautyAsync();
            var backups = app.Get<BackupService>();
            var status = await backups.GetStatusAsync();
            Assert.True((await backups.ConfigureAsync(shared, status.Schedule, status.OnExit, status.Retention)).IsSuccess);
            for (var i = 0; i < 3; i++)
            {
                Assert.True((await backups.CreateBackupAsync(BackupTrigger.Manual)).IsSuccess);
                app.Time.Advance(TimeSpan.FromDays(40));
            }

            Assert.True(File.Exists(Path.Combine(shared, foreign)));
        }
        finally
        {
            Directory.Delete(shared, recursive: true);
        }
    }

    [Fact]
    public async Task A_safety_backup_before_restore_cannot_be_requested_from_outside()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        Assert.Equal("validation.backup.trigger", (await app.Get<BackupService>().CreateBackupAsync(BackupTrigger.PreRestore)).Error.Code);
    }

    [Fact]
    public async Task A_fresh_server_license_undoes_a_clock_that_was_once_set_a_year_ahead()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var licensing = new LicensingOptions { PublicKeys = new Dictionary<string, byte[]> { ["t1"] = key.ExportSubjectPublicKeyInfo() } };
        await using var app = await TestApp.CreateAsync(licensing: licensing);
        await app.SetupBeautyAsync();
        var license = app.Get<LicenseService>();

        app.Time.Advance(TimeSpan.FromDays(365)); // clock wrong for a moment…
        await license.RefreshAsync();
        app.Time.Advance(TimeSpan.FromDays(-365)); // …and fixed

        var now = app.Time.GetUtcNow().UtcDateTime;
        var signed = LicenseVerifier.Sign(new LicenseDocument
        {
            LicenseId = "lic_1", Product = "vernuko-desktop", Edition = "Standart", ActivationId = "act_1", DeviceHash = app.Device.DeviceHash,
            IssuedAtUtc = now, ValidUntilUtc = now.AddDays(180), RefreshAfterUtc = now.AddDays(7),
        }, key, "t1");
        var stored = await license.StoreFromServerAsync(signed, activation: true);
        Assert.True(stored.IsSuccess, stored.IsFailure ? stored.Error.ToString() : null);
        Assert.Equal(AccessMode.Full, stored.Value.Mode);

        // An old license file cannot be used to rewind the clock protection.
        app.Time.Advance(TimeSpan.FromDays(-200));
        await license.RefreshAsync();
        Assert.True(license.Current.ClockRollbackSuspected);
    }

    [Fact]
    public void A_license_file_with_missing_fields_is_reported_as_malformed()
    {
        var verifier = new LicenseVerifier(new Dictionary<string, byte[]>());
        Assert.Equal(LicenseErrors.Malformed.Code, verifier.Verify("""{"payload":"x","sig":"y","alg":"ES256"}""").Error.Code);
        Assert.Equal(LicenseErrors.Malformed.Code, verifier.Verify("""{"kid":"k","alg":"ES256"}""").Error.Code);
    }

    [Fact]
    public async Task An_unreadable_attachment_key_is_never_replaced_by_a_new_one()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var notes = app.Get<PrivateNotesService>();
        Assert.True((await notes.AddAsync(customer, "İlk not")).IsSuccess);

        // As after a Windows password reset: the stored key can no longer be opened.
        await using (var db = await app.DbAsync())
        {
            await SettingsStore.SetAsync(db, AttachmentKeyStore.ProtectedKeySetting, Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), DateTime.UtcNow, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        string before;
        await using (var db = await app.DbAsync())
        {
            before = (await SettingsStore.GetAsync(db, AttachmentKeyStore.ProtectedKeySetting, CancellationToken.None))!;
        }

        Assert.Equal("attachments.key_unavailable", (await notes.AddAsync(customer, "İkinci not")).Error.Code);
        await using (var db = await app.DbAsync())
        {
            Assert.Equal(before, await SettingsStore.GetAsync(db, AttachmentKeyStore.ProtectedKeySetting, CancellationToken.None));
        }
    }
}
