namespace Vernuko.Persistence;

/// <summary>Key/value store for installation-level state (installation id, license cache, backup settings ...).</summary>
public sealed class AppSetting
{
    private AppSetting()
    {
    }

    public AppSetting(string key, string value, DateTime updatedAtUtc)
    {
        Key = key;
        Value = value;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}

public static class SettingKeys
{
    public const string InstallationId = "installation.id";
    public const string LicenseTrialStartedUtc = "license.trial_started_utc";
    public const string LicenseHighWaterMarkUtc = "license.high_water_mark_utc";
    public const string LicenseSigned = "license.signed";
    public const string BackupKdfSalt = "backup.kdf_salt";
    public const string BackupProtectedKek = "backup.protected_kek";
    public const string BackupKeyId = "backup.key_id";
    public const string BackupFolder = "backup.folder";
    public const string BackupSchedule = "backup.schedule";
    public const string BackupOnExit = "backup.on_exit";
    public const string BackupRetention = "backup.retention";
    public const string BackupLastSuccessUtc = "backup.last_success_utc";
    public const string BackupLastFile = "backup.last_file";
    public const string BackupLastError = "backup.last_error";
    public const string BackupRecoveryKeyConfirmed = "backup.recovery_key_confirmed";
    public const string BackupCloudFolder = "backup.cloud.folder";
    public const string BackupCloudProvider = "backup.cloud.provider";
    public const string BackupCloudConsentUtc = "backup.cloud.consent_utc";
    public const string BackupCloudLastSuccessUtc = "backup.cloud.last_success_utc";
    public const string BackupCloudLastError = "backup.cloud.last_error";
}
