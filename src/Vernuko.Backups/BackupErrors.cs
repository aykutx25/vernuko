using Vernuko.Core;

namespace Vernuko.Backups;

public static class BackupErrors
{
    public static readonly Error RecoveryKeyFormat = new("backup.recovery_key_format", "Kurtarma anahtarı biçimi hatalı. 8 grup halinde 32 karakter olmalıdır.");
    public static readonly Error WrongKey = new("backup.wrong_key", "Kurtarma anahtarı bu yedeği açamadı. Anahtarı kontrol edin.");
    public static readonly Error NotABackup = new("backup.not_a_backup", "Seçilen dosya bir Vernuko yedeği değil.");
    public static readonly Error UnsupportedVersion = new("backup.version", "Bu yedek, Vernuko'nun daha yeni bir sürümüyle oluşturulmuş. Önce uygulamayı güncelleyin.");
    public static readonly Error Corrupted = new("backup.corrupted", "Yedek dosyası bozuk veya eksik. Başka bir yedek seçin.");
    public static readonly Error ManifestMismatch = new("backup.manifest", "Yedek içeriği doğrulanamadı (bütünlük kontrolü başarısız).");
    public static readonly Error NoKeyConfigured = new("backup.no_key", "Yedekleme anahtarı henüz oluşturulmamış. Ayarlar > Yedekleme bölümünden yedeklemeyi etkinleştirin.");
}
