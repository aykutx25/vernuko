# C. Veritabanı Mimarisi

## Motor ve çalışma modeli
- **Model A (Faz 1):** SQLite 3 (Microsoft.Data.Sqlite + EF Core 10), dosya: `%LOCALAPPDATA%\Vernuko\Data\vernuko.db`. WAL modu, `synchronous=FULL` (finansal kayıtlar elektrik kesintisinde kaybolmasın), `foreign_keys=ON`.
- **Her yazma işlemi `BEGIN IMMEDIATE` işlemi içinde** yapılır (Microsoft.Data.Sqlite varsayılanı). Böylece "çakışma kontrolü + kayıt" atomiktir; aynı dosyaya yazan ikinci bir süreç bile araya giremez (test: `Concurrent_bookings_for_same_slot_only_one_wins`).
- **Model B/C (Faz 7):** SQLite dosyası **ağ üzerinden paylaşılmaz**. Yerel ağ ve bulut sürümünde merkezi servis + PostgreSQL kullanılır. Veri modeli buna hazırdır (aşağıya bakın).

## Tasarım ilkeleri
| İlke | Uygulama |
|---|---|
| Küresel benzersiz kimlik | Tüm kayıtlar **GUID v7** (zaman sıralı, indeks dostu; çok şube/senkronda çakışmaz). Kullanıcıya gösterilen sıralı müşteri numarası ayrı (`Customers.Number`, unique). |
| Zaman | Tüm anlar **UTC** saklanır (`*Utc` sütunları, dönüştürücü Kind=Utc zorlar). İşletmenin IANA saat dilimi (`BusinessSettings.TimeZoneId`, varsayılan `Europe/Istanbul`) gün sınırları ve gösterim için kullanılır; yaz saati geçişleri test edilir. Randevular dakika hassasiyetindedir. |
| Para | `long` **kuruş** (`*Minor`). SQLite'ta tam SUM; kayan nokta hatası yok. Para birimi kodu saklanır (`TRY`). |
| Enum | Metin olarak saklanır (sıra değişse bozulmaz). |
| Eşzamanlılık | Değişebilen kayıtlarda `Version` iyimser eşzamanlılık belirteci; eski ekrandan kaydetme reddedilir. |
| Şube | Şubeye bağlı tablolarda `BranchId` ilk günden var (`Branches`, varsayılan şube sabit kimlikli). |
| Silme | Müşteri silinmez: **arşiv** veya **anonimleştirme**. Finansal kayıt, seans defteri ve denetim kaydı **append-only** (tetikleyiciyle zorunlu). |

## Tablolar
| Tablo | Amaç | Önemli alanlar / kurallar |
|---|---|---|
| `BusinessSettings` | Tek satır işletme ayarı | Profil, ad, iletişim, saat dilimi, para birimi, haftalık çalışma saatleri (JSON), randevu aralığı, gelmeme politikası, otomatik kilit, demo bayrağı |
| `Branches` | Şubeler | Faz 1'de tek kayıt |
| `Users` | Kullanıcılar | `Username` unique (NOCASE), parola karması (PBKDF2), rol, ek/çıkarılan izinler, kilit sayacı |
| `Customers` | Müşteri kartı | `Number` unique, `Phone` (E.164) indeksli, `SearchKey` (Türkçe katlanmış ad + telefon varyantları) indeksli, iletişim tercihleri, **pazarlama izni ayrı** (tarih + kaynak), durum (Aktif/Arşiv/Anonim), T.C. kimlik isteğe bağlı |
| `Services` | Hizmetler | Süre, hazırlık, temizlik, fiyat, gereken kaynak türü |
| `Staff` | Personel | Şube, kişisel çalışma saatleri (JSON, boşsa işletme saatleri) |
| `StaffServices` | Personel–hizmet | Bileşik PK; boşsa herkes verebilir |
| `Resources` | Oda/cihaz/koltuk/ünit/lift/stüdyo | Tür, kapasite (1 = özel kullanım) |
| `ScheduleExceptions` | İzin, resmî tatil, kapalı gün, bakım | Kapsam (işletme/personel/kaynak), UTC aralık |
| `Appointments` | Randevu | Müşteri, hizmet, personel, başlangıç/bitiş (UTC), hazırlık/temizlik, durum, fiyat anlık görüntüsü, paket, bölge, not, kaynak, iptal nedeni, zaman damgaları; indeksler: (BranchId, StartUtc), (CustomerId, StartUtc), (StaffMemberId, StartUtc), CustomerPackageId |
| `AppointmentBookings` | Kaynak işgali | Randevu başına personel + her oda/cihaz için bir satır; `BlockStartUtc/BlockEndUtc` tamponlarla; `IsActive`; indeks (ResourceId, BlockStartUtc, BlockEndUtc); **çakışma tetikleyicisi** |
| `AppointmentStatusChanges` | Durum geçmişi | Kim, ne zaman, nereden nereye, neden |
| `PackageTemplates` | Paket tanımları | Hizmet, seans sayısı, fiyat, geçerlilik |
| `CustomerPackages` | Satılan paketler | Toplam seans, bölge, satın alma/son kullanma, durum (Aktif/Dondurulmuş/İptal) — **kalan hak saklanmaz, defterden hesaplanır** |
| `SessionLedger` | Seans defteri (append-only) | Kullanıldı (−1, randevuya bağlı), İade (+1), Düzeltme (±n, gerekçe zorunlu) |
| `CustomerTransactions` | Cari hesap (append-only) | Tür (Borç/Tahsilat/İade/İndirim/Ters kayıt), tutar, saklanan bakiye etkisi, yöntem, kapora, randevu/paket bağlantısı, **IdempotencyKey unique**, `ReversesTransactionId` unique |
| `AuditEntries` | Denetim günlüğü (append-only) | Sıra no (unique), kullanıcı, işlem, varlık, özet (kişisel veri içermez), **hash zinciri** (`PreviousHash`, `Hash`) |
| `AppSettings` | Kurulum durumu (anahtar/değer) | Kurulum kimliği, lisans önbelleği ve yüksek su işareti, yedek ayarları, DPAPI ile korunan yedek anahtarı, DPAPI ile ve yedek anahtarıyla sarılı dosya anahtarı |
| `CommissionRules` *(Faz 2)* | Prim kuralları | Tür (hizmet/paket satışı/ürün satışı), personel (boş = herkes), hizmet/ürün (boş = hepsi), yüzde (baz puan) veya sabit tutar; en özel kural geçerli |
| `CommissionEntries` *(Faz 2, append-only)* | Hesaplanmış primler | Personel, tür, kaynak (randevu/paket/satış), baz tutar, prim tutarı, **IdempotencyKey unique**, `ReversesEntryId` unique (iptal/geri almada ters kayıt) |
| `Products` *(Faz 2)* | Ürün/malzeme | Ad, kod, birim, satış fiyatı, maliyet, kritik seviye — **stok saklanmaz, hareketlerden hesaplanır** |
| `StockMovements` *(Faz 2, append-only)* | Stok hareketleri | Giriş, satış, hizmette kullanım, sayım düzeltmesi, iade, ters kayıt; randevu/müşteri bağlantısı; `ReversesMovementId` unique |
| `ServiceMaterials` *(Faz 2)* | Hizmet malzemeleri | Hizmet tamamlanınca düşülecek ürün ve miktar |
| `Waitlist` *(Faz 2)* | Bekleme listesi | Müşteri, hizmet/personel tercihi, tarih aralığı, tercih edilen zaman (serbest metin), durum |
| `ConsentTemplates` *(Faz 2)* | Onam formları | Başlık, metin, **metin sürümü** (metin değişince artar) |
| `CustomerConsents` *(Faz 2)* | Müşteri onayları | Form + kabul edilen sürüm ve metnin SHA-256 özeti, yöntem, tarih, kaydeden, geri alma tarihi/gerekçesi |
| `CustomerFiles` *(Faz 2)* | Şifreli dosya meta verisi | Tür (fotoğraf/imzalı onam/belge), özgün ad, boyut, SHA-256, `StoredName` unique; içerik `attachments\` klasöründe AES-256-GCM ile |
| `Appointments.SeriesId` *(Faz 2)* | Tekrarlayan seri | Aynı serideki randevular ortak kimlik taşır; seri iptali/taşıması yalnızca açık randevuları etkiler |

İlişkiler: randevu → müşteri/hizmet/personel/paket/şube (Restrict: ana kayıt silinemez); randevu → booking/geçmiş (Cascade); paket → defter (Restrict).

## Veritabanı seviyesinde bütünlük kuralları (uygulama kodundan bağımsız)
`src/Vernuko.Persistence/DatabaseTriggers.cs`, ilk migration ile kurulur:

1. **Çakışma tetikleyicisi:** `AppointmentBookings` tablosunda aynı `ResourceId` için aktif ve özel kullanımlı iki kaydın zaman aralıkları kesişemez (INSERT ve UPDATE). İhlalde `VERNUKO_BOOKING_OVERLAP` hatası. Uygulama katmanındaki kontrol atlansa bile çift rezervasyon yazılamaz (test: `Database_trigger_blocks_overlaps_even_if_application_check_is_bypassed`).
2. **Append-only:** `CustomerTransactions`, `SessionLedger`, `AuditEntries` (ilk migration) ve `CommissionEntries`, `StockMovements` (`Phase2Operations` migration'ı) tablolarında UPDATE/DELETE reddedilir (`VERNUKO_APPEND_ONLY`). İlk migration'daki tetikleyici listesi dondurulmuştur; yeni tablolar yeni migration ile eklenir. Düzeltme yalnızca yeni kayıtla (ters kayıt / iade / düzeltme) yapılır.
3. **Benzersizlik:** ödeme idempotency anahtarı, bir işlemin yalnızca bir ters kaydı olabilmesi, kullanıcı adı, müşteri numarası, randevu başına kaynak.

## Migration (şema geçişi) güvenliği
`DatabaseInitializer`:
1. Veritabanında uygulamanın tanımadığı bir migration varsa (**daha yeni sürümle yazılmış**) açmayı reddeder; veri değiştirilmez.
2. Bekleyen migration varsa ve veritabanı doluysa önce `VACUUM INTO` ile tutarlı **güvenlik kopyası** alır (`safety/premigration-*.db`).
3. Migration başarısız olursa dosyaları güvenlik kopyasıyla geri yükler ve açık bir hata verir.
Test: `Failed_migration_rolls_back_to_snapshot_and_newer_database_is_refused`.

Yeni migration: `dotnet tool restore && dotnet ef migrations add <Ad> --project src/Vernuko.Persistence --startup-project src/Vernuko.Persistence --output-dir Migrations`.

## Performans
- Arama: `SearchKey LIKE '%terim%'` + telefon/ad indeksleri. 20.000 müşteride arama testi (Linux CI, bellek içi değil gerçek dosya) 1,5 sn eşiğinin altında kalmalı; gerçek cihaz ölçümleri Faz 8'de raporlanacak. Not: `%terim%` ön ek indeks kullanmaz; 100 bin+ kayıtta SQLite FTS5'e geçiş planlanmıştır (P1).
- Günlük/haftalık takvim sorguları `(BranchId, StartUtc)` aralık indeksini kullanır; liste görünümleri sanallaştırılmıştır (WPF).

## PostgreSQL'e geçiş (Faz 7) için notlar
- Çakışma tetikleyicisi yerine `btree_gist` + `EXCLUDE USING gist (resource_id WITH =, tstzrange(block_start, block_end) WITH &&) WHERE (is_active)`.
- Append-only kuralları rol izinleri + tetikleyicilerle.
- GUID v7, UTC, kuruş ve metin enumlar doğrudan taşınır; SQLite → PostgreSQL veri taşıma aracı Faz 7 işi.
