using Vernuko.Application.Services;
using Vernuko.Backups;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

public class CloudBackupTests
{
    [Fact]
    public void Locator_finds_onedrive_google_drive_and_dropbox_folders()
    {
        var root = Directory.CreateTempSubdirectory("vernuko-cloud-").FullName;
        try
        {
            var profile = Directory.CreateDirectory(Path.Combine(root, "Users", "Ayse")).FullName;
            var oneDrive = Directory.CreateDirectory(Path.Combine(profile, "OneDrive")).FullName;
            var gdriveRoot = Directory.CreateDirectory(Path.Combine(root, "G")).FullName;
            var myDrive = Directory.CreateDirectory(Path.Combine(gdriveRoot, "Drive'ım")).FullName; // Turkish Windows
            var dropbox = Directory.CreateDirectory(Path.Combine(root, "D", "Dropbox (Kişisel)")).FullName;
            var local = Directory.CreateDirectory(Path.Combine(profile, "AppData", "Local")).FullName;
            Directory.CreateDirectory(Path.Combine(local, "Dropbox"));
            File.WriteAllText(Path.Combine(local, "Dropbox", "info.json"),
                "{\"personal\":{\"path\":" + System.Text.Json.JsonSerializer.Serialize(dropbox) + ",\"host\":1}}");

            var env = new Dictionary<string, string?> { ["OneDrive"] = oneDrive, ["OneDriveConsumer"] = oneDrive };
            var locator = new CloudFolderLocator(name => env.GetValueOrDefault(name), profile, local, Path.Combine(profile, "AppData", "Roaming"),
                () => [(gdriveRoot, "Google Drive"), (root, "Yerel Disk")]);

            var found = locator.Detect();
            Assert.Contains(found, c => c.Provider == CloudProvider.OneDrive && c.Path == oneDrive);
            Assert.Contains(found, c => c.Provider == CloudProvider.GoogleDrive && c.Path == myDrive);
            Assert.Contains(found, c => c.Provider == CloudProvider.Dropbox && c.Path == dropbox);
            Assert.Single(found, c => c.Provider == CloudProvider.OneDrive); // same folder via two variables
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Dropbox_info_json_with_personal_and_business_accounts()
    {
        var paths = CloudFolderLocator.ParseDropboxInfo("{\"personal\":{\"path\":\"C:\\\\Users\\\\a\\\\Dropbox\"},\"business\":{\"path\":\"C:\\\\Users\\\\a\\\\Dropbox (Firma)\"}}");
        Assert.Equal(["C:\\Users\\a\\Dropbox", "C:\\Users\\a\\Dropbox (Firma)"], paths);
        Assert.Empty(CloudFolderLocator.ParseDropboxInfo("[]"));
    }

    [Fact]
    public async Task Cloud_copy_needs_consent_copies_every_backup_survives_a_missing_folder_and_restores()
    {
        await using var app = await TestApp.CreateAsync();
        var key = (await app.SetupBeautyAsync()).RecoveryKey!;
        var backups = app.Get<BackupService>();
        var cloudRoot = Directory.CreateDirectory(Path.Combine(app.Root, "OneDrive")).FullName;

        // KVKK acknowledgement is required.
        var noConsent = await backups.EnableCloudCopyAsync(cloudRoot, CloudProvider.OneDrive, consentGiven: false);
        Assert.Equal("validation.backup.cloud_consent", noConsent.Error.Code);
        Assert.True((await backups.EnableCloudCopyAsync(cloudRoot, CloudProvider.OneDrive, consentGiven: true)).IsSuccess);
        var status = await backups.GetStatusAsync();
        Assert.True(status.CloudEnabled);
        Assert.Equal("OneDrive", status.CloudText);

        // Every regular backup also lands, encrypted and complete, in the cloud folder.
        var customer = await app.AddCustomerAsync("Bulut", "Müşterisi", "0532 999 88 77");
        var run = (await backups.CreateBackupAsync(BackupTrigger.Manual)).Value;
        Assert.Equal("bulut klasörüne de kopyalandı", run.CloudNote);
        var cloudFile = Path.Combine(cloudRoot, BackupService.CloudSubFolder, run.FileName);
        Assert.True(File.Exists(cloudFile));
        Assert.Equal(new FileInfo(Path.Combine(app.Root, "backups", run.FileName)).Length, new FileInfo(cloudFile).Length);
        Assert.NotNull((await backups.GetStatusAsync()).CloudLastSuccessUtc);

        // The cloud copy restores on a new computer with the recovery key.
        await using (var newPc = await TestApp.CreateAsync(protector: new TestSecretProtector()))
        {
            await newPc.SetupBeautyAsync(enableBackup: false);
            Assert.True((await newPc.Get<BackupService>().RestoreAsync(cloudFile, key)).IsSuccess);
            await newPc.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword);
            Assert.Contains((await newPc.Get<CustomerService>().SearchAsync("Bulut")).Value, c => c.Id == customer);
        }

        // Cloud folder gone (app uninstalled, drive not mounted): the local backup still succeeds, the problem is shown.
        Directory.Delete(cloudRoot, true);
        var second = await backups.CreateBackupAsync(BackupTrigger.Manual);
        Assert.True(second.IsSuccess);
        Assert.StartsWith("bulut kopyası yapılamadı", second.Value.CloudNote);
        var after = await backups.GetStatusAsync();
        Assert.False(string.IsNullOrEmpty(after.CloudLastError));
        Assert.Contains("son kopya başarısız", after.CloudText);

        // Turning it off stops copying.
        Assert.True((await backups.DisableCloudCopyAsync()).IsSuccess);
        Assert.Null((await backups.CreateBackupAsync(BackupTrigger.Manual)).Value.CloudNote);
        Assert.False((await backups.GetStatusAsync()).CloudEnabled);
    }

    [Fact]
    public async Task Secretary_cannot_enable_the_cloud_copy()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        await app.Get<UserAdminService>().CreateAsync("sekreter", "Sekreter", "Randevu-Masasi-1", Security.SystemRole.Secretary);
        app.Get<AuthService>().Logout();
        await app.Get<AuthService>().LoginAsync("sekreter", "Randevu-Masasi-1");
        var r = await app.Get<BackupService>().EnableCloudCopyAsync(app.Root, CloudProvider.Other, consentGiven: true);
        Assert.Equal(Core.Error.Forbidden, r.Error);
    }
}
