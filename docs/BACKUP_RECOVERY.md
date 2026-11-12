# E. Yedekleme ve Kurtarma Mimarisi

Kaynaklı API araştırması: [research/messaging-and-backup-apis.md](research/messaging-and-backup-apis.md) (bölüm 2–5).

## Durum
| Bileşen | Durum |
|---|---|
| Yerel / harici disk / USB klasörüne şifreli yedek | **Uygulandı ve test edildi** |
| Doğrulama (test geri yükleme), geri yükleme, kurtarma anahtarı | **Uygulandı ve test edildi** |
| Saklama politikası (7 günlük / 4 haftalık / 3 aylık) | **Uygulandı ve test edildi** |
| Zamanlanmış yedek (uygulama açıkken) ve kapanışta yedek | Uygulandı (masaüstü zamanlayıcısı; Windows'ta çalıştırılarak doğrulanmalı) |
| Google Drive, OneDrive, Dropbox *(Faz 4, uygulandı)* | **Eşitleme klasörü yöntemi:** her düzenli yedek (elle, zamanlanmış, kapanışta, güncelleme öncesi) şifreli haliyle bilgisayardaki OneDrive / Google Drive / Dropbox klasörüne (`<klasör>\\Vernuko Yedekler`) de kopyalanır; yüklemeyi sağlayıcının kendi uygulaması müşterinin hesabına yapar. API anahtarı veya uygulama kaydı gerekmez; Vernuko'nun bulut hesabına erişimi yoktur. Klasörler otomatik bulunur (OneDrive ortam değişkenleri, "Google Drive" etiketli sürücüdeki "My Drive/Drive'ım", Dropbox `info.json`) veya elle seçilir. Açmak için KVKK bilgilendirmesinin onaylanması gerekir (onay zamanı ve denetim kaydı tutulur). Aynı saklama politikası bulutta da uygulanır. Bulut klasörüne erişilemezse yerel yedek yine başarılı sayılır; hata Ayarlar'da ve durum çubuğunda görünür. Testler: `CloudBackupTests`. **API ile doğrudan bağlantı (OAuth) yapılmadı:** sahibin Google Cloud, Microsoft Entra ve Dropbox'ta uygulama kaydı açmasını gerektirir (aşağıdaki tasarım). |
| Fotoğraf / ek dosyalar *(Faz 2)* | Müşteri fotoğrafları ve belgeleri, diskteki **şifreli** halleriyle (`att_<ad>` girdileri) arşive eklenir; arşiv ayrıca bütünüyle şifrelenir. Dosya anahtarı yedek anahtarıyla sarılı olarak veritabanında durduğu için yeni bilgisayarda kurtarma anahtarıyla geri yüklenince fotoğraflar da açılır (test: `Consents_and_encrypted_photos_with_permissions_backup_and_anonymization`). Geri yüklemede mevcut dosya klasörü önce güvenlik kopyası olarak kenara alınır. Doğrulama mesajı dosyaların dahil olup olmadığını söyler. |

## Akış
```
Canlı DB ──VACUUM INTO──▶ tutarlı anlık görüntü (temp)
          ──ZIP (Deflate)──▶ manifest.json (SHA-256 + boyut) + vernuko.db
          ──AES-256-GCM STREAM──▶ vernuko-YYYYMMDD-HHMMSS-<tür>-<rastgele>.vbk
          ──.partial + atomik taşıma──▶ hedef klasör ──▶ saklama politikası
```
- **Tutarlılık:** `VACUUM INTO` canlı veritabanından (WAL dahil) işlemsel tutarlı kopya üretir; .db dosyası asla doğrudan kopyalanmaz.
- **Atomik yazım:** önce `.partial`, flush, sonra yeniden adlandırma; yarım kalan dosyalar bir sonraki yedekte temizlenir.
- **Dosya adı** kişisel veri içermez; son ek, yedek kimliğinin rastgele kısmıdır (aynı saniyede iki yedek çakışmaz — test edilerek bulunup düzeltilen bir hata).

## Şifreleme
| Öğe | Seçim |
|---|---|
| Algoritma | AES-256-GCM, **STREAM yapısı**: 1 MiB parçalar, nonce = 7 bayt rastgele önek ‖ 4 bayt sayaç ‖ 1 bayt son-parça bayrağı |
| Bağlam doğrulama | Başlığın SHA-256'sı her parçanın AAD'si → başlık, sıra, kesilme, ekleme, değiştirme tespit edilir |
| Dosya anahtarı | Her yedek için rastgele 256-bit anahtar; KEK ile AES-GCM'de sarılır (AAD: yedek kimliği) |
| KEK | Kurtarma anahtarından **HKDF-SHA256** ile (tuz kurulum başına). Kurtarma anahtarı 160 bit rastgele olduğu için parola KDF'i (PBKDF2/Argon2) gerekmez. |
| Kurtarma anahtarı | 32 karakter, 8×4 grup, karışabilen harfler (I/O/0/1) yok; okuma hataları affedilir. Yalnızca bir kez gösterilir; kullanıcı geri yazarak doğrular. **Saklanmaz.** |
| Yerel anahtar | KEK, Windows **DPAPI** (CurrentUser) ile korunarak veritabanında saklanır → aynı bilgisayarda otomatik yedek/geri yükleme anahtar sormadan çalışır |
| Başka bilgisayar | DPAPI açılamaz → kurtarma anahtarı istenir; geri yüklemeden sonra KEK bu bilgisayarın DPAPI'siyle yeniden korunur |

Başlık (`BackupHeader`) şifresizdir ama **kişisel veri içermez** (biçim sürümü, yedek kimliği, tarih, uygulama/şema sürümü, kurulum kimliği, tetikleyici, anahtar kimliği, tuz, sarılmış anahtar, nonce öneki). İşletme adı yalnızca şifreli manifest içindedir.

**Bulut sağlayıcısı yedeği okuyamaz:** buluta yalnızca şifreli `.vbk` dosyası gider. Ancak **şifreleme tek başına KVKK uyumu anlamına gelmez** — yurt dışı bulut, KVKK rehberinde aktarım sayılıyor (bkz. SECURITY.md › KVKK).

## Geri yükleme
1. Şifre çöz + kimlik doğrula (yanlış anahtar → "Kurtarma anahtarı bu yedeği açamadı"; bozuk/kesik dosya → "Yedek dosyası bozuk veya eksik").
2. ZIP'i aç, her dosyayı manifestteki SHA-256 ve boyutla doğrula (zip-slip koruması).
3. `PRAGMA integrity_check`.
4. Şema uyumluluğu: yedek daha yeni sürümdense reddet ("önce uygulamayı güncelleyin").
5. **Mevcut verinin güvenlik kopyası:** düz anlık görüntü (`safety/prerestore-*.db`) + anahtar varsa şifreli `prerestore` yedeği.
6. Dosyaları değiştir (başarısızlıkta güvenlik kopyası geri konur).
7. Eski yedekse migration uygula (kendi güvenlik kopyasıyla).
8. Geri yüklenen veriye "backup.restored" denetim kaydı; kullanıcı yeniden giriş yapar.

## Saklama (GFS)
Varsayılan: son 7 günün her birinden en yeni, son 4 haftanın her birinden en yeni, son 3 ayın her birinden en yeni; **en yeni yedek her zaman korunur**. Güvenlik yedekleri (migration öncesi, geri yükleme öncesi, güncelleme öncesi) bu politika tarafından silinmez. Kullanıcı sayıları değiştirebilir.

## Zamanlama
- Günlük/haftalık/aylık: **yalnızca Vernuko açıkken** kontrol edilir (açılışta + 30 dakikada bir). Uygulama, kapalı bir bilgisayarı yedekliyormuş gibi davranmaz.
- Kapanışta yedek (isteğe bağlı, varsayılan açık).
- Son başarılı yedek ve son hata durum çubuğunda ve ana sayfada gösterilir.

## Bulut sağlayıcı adaptörleri (Faz 4 tasarımı)
Ortak arayüz: `IBackupTarget` (`List`, `Upload`, `Download`, `Delete`, `GetQuota`). Hedefler yalnızca şifreli dosya alır.

| Sağlayıcı | Kimlik doğrulama | Kapsam / izin | Yükleme | Not |
|---|---|---|---|---|
| Google Drive | OAuth 2.0 yüklü uygulama: sistem tarayıcısı + loopback + PKCE (gömülü webview yasak) | `drive.file` (non-sensitive, CASA gerekmez) | Resumable upload, 10 MiB parça (256 KiB katı) | Uygulama "In production" olmalı (testte refresh token 7 gün); kota satıcı projesine yazılır |
| OneDrive | MSAL (Windows'ta WAM), public client | `Files.ReadWrite.AppFolder` (kişisel hesapta kesin; iş/okul hesabında test edilmeli) | Upload session, 10 MiB parça (320 KiB katı) | `special/approot` |
| Dropbox | OAuth PKCE + `token_access_type=offline` | App Folder | Upload session (start/append/finish) | 50 bağlı kullanıcıdan sonra 2 hafta içinde production onayı; sabit loopback portları kaydedilmeli; Dropbox.Api 7.4.0 (MIT) |

Ortak gereksinimler: refresh token'lar DPAPI ile korunur; kesintide kaldığı yerden devam; yükleme sonrası boyut/özet kontrolü; kota uyarısı ve yedek boyutu tahmini; API hız limitleri için üstel geri çekilme; birden fazla hedef aynı anda tanımlanabilir.

## Felaket senaryoları
| Senaryo | Sonuç |
|---|---|
| Bilgisayar bozuldu / değişti | Yeni bilgisayara kur → kurulum sihirbazının ilk ekranında **Yedekten geri yükle…** → .vbk dosyası + kurtarma anahtarı → eski kullanıcı adı ve parolayla giriş. Kurulum adımları yeniden yapılmaz; kullanıcılar, müşteriler, paketler, ödemeler ve fotoğraflar gelir; yedekleme yeni Windows hesabı için yeniden korunur. Bu yol yalnızca henüz kurulmamış (kullanıcısı olmayan) bir bilgisayarda açıktır; sonrasında geri yükleme yetkili kullanıcıyla Ayarlar › Yedekleme'den yapılır (test: `New_computer_restores_a_backup_from_the_setup_wizard_and_signs_in_with_old_credentials`) |
| Kurtarma anahtarı kayıp, eski bilgisayar çalışıyor | Ayarlar › Yeni kurtarma anahtarı oluştur (eski yedekler eski anahtarla açılır; yeni yedek alın) |
| Kurtarma anahtarı kayıp, bilgisayar yok | Yedekler açılamaz. Bu bilinçli bir tasarım; anahtar Vernuko tarafından saklanmaz. |
| Yedek dosyası bozuk | Doğrulama reddeder, canlı veri etkilenmez; bir önceki yedek seçilir |
| Migration hatası | Güncelleme öncesi kopyaya otomatik dönüş |

## Testler
`BackupCryptoTests` (boyutlar, yanlış anahtar, bayt değişimi, kesilme, ekleme, başlık kurcalama, kurtarma anahtarı, saklama, adlandırma, yol güvenliği) ve `BackupRestoreTests` (uçtan uca geri yükleme, yeni bilgisayarda kurtarma anahtarı, bozuk yedek, anahtar doğrulama ve zamanlama, anahtarsız yedek).
