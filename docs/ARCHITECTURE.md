# Mimari

## Teknoloji kararları (Kasım 2026'da doğrulandı)
| Konu | Seçim | Not |
|---|---|---|
| Çalışma zamanı | **.NET 10 LTS** (destek: 14 Kasım 2028) | .NET 8 ve 9 desteği 10 Kasım 2026'da bitti; .NET 11 STS olduğu için seçilmedi. EF Core 11 yalnızca .NET 11'i hedefler → EF Core 10.0.x. |
| Arayüz | WPF + MVVM (CommunityToolkit.Mvvm 8.4, MIT) | View model'ler platformdan bağımsız projede (Linux'ta test edilebilir) |
| Veri | EF Core 10 + SQLite (Microsoft.Data.Sqlite; SQLitePCLRaw Apache-2.0; SQLite kamu malı) | Model B/C için PostgreSQL (Faz 7) |
| DI / logging | Microsoft.Extensions.* (MIT), Serilog + Serilog.Sinks.File (Apache-2.0) | Günlükler 14 gün, 10 MB/dosya |
| Kripto | Yalnızca .NET BCL: AES-GCM, HKDF, PBKDF2, ECDSA P-256, DPAPI | Üçüncü taraf kripto bağımlılığı yok |
| Test | xUnit v3 (Apache-2.0), gerçek SQLite dosyaları | 120+ test |
| Kurulum/güncelleme | **Velopack 1.2.161 (MIT)** — kullanıcı başına kurulum (yönetici yetkisi yok), delta güncelleme, `Program.Main` içinde `VelopackApp.Run()`. Paket kimliği `VernukoApp` (veri klasörü `Vernuko` ile çakışmaz; kaldırma veriyi silmez). Ayrıntı: [RELEASE.md](RELEASE.md) | bkz. research/licensing-payments-signing.md |

Tüm NuGet paketleri `Directory.Packages.props` içinde merkezi olarak sabitlenir; NuGet güvenlik denetimi (`NuGetAudit`, tüm bağımlılıklar, düşük seviye) açıktır. Analizörler en son önerilen seviyede ve uyarılar hata olarak derlenir.

## Modüler monolit
Projeler ayrı servis değildir; tek süreçte çalışır. Sınırlar derleyici tarafından korunur (domain modülleri yalnızca `Vernuko.Core`'a bağımlıdır).

```
                         Vernuko.Desktop (WPF, net10.0-windows)
                           │  Views, tema, diyaloglar, smoke test
                           ▼
            Vernuko.Presentation (MVVM view model'ler, net10.0 — test edilebilir)
                           ▼
 Vernuko.Infrastructure ─▶ Vernuko.Application (kullanım senaryoları, yetki, lisans kapısı, denetim)
 (DPAPI, cihaz kimliği,     │        │                │
  yollar, Serilog)          ▼        ▼                ▼
                   Vernuko.Persistence    Vernuko.Messaging   Vernuko.Backups   Vernuko.Reporting
                   (DbContext, migration, (wa.me, şablonlar,  (format, kripto,  (özetler)
                    tetikleyiciler)        ISmsProvider)       saklama, hedef)
                           ▼
   Vernuko.Customers · Vernuko.Appointments · Vernuko.Packages · Vernuko.Payments
   Vernuko.BusinessProfiles · Vernuko.Security · Vernuko.Licensing
                           ▼
                     Vernuko.Core (Result, Money, PhoneNumber, TurkishText, TimeZones, UtcRange, WeeklySchedule, Ids)
```

| Proje | Sorumluluk |
|---|---|
| Vernuko.Core | Ortak çekirdek: `Result/Error`, GUID v7, para (kuruş), telefon (E.164), Türkçe metin katlama, saat dilimi/DST, haftalık çalışma saatleri |
| Vernuko.Security | Kullanıcı, rol, 34 işlem izni, PBKDF2 parola karması, parola politikası, hesap kilidi, boşta kilit politikası, denetim kaydı + hash zinciri |
| Vernuko.BusinessProfiles | 14 meslek profili kataloğu, işletme ayarları, şube |
| Vernuko.Customers | Müşteri kartı, doğrulamalar, T.C. kimlik sağlaması, mükerrer tespiti, arşiv/anonimleştirme; onam formu/kaydı ve şifreli dosya meta verisi (Faz 2) |
| Vernuko.Appointments | Hizmet, personel, kaynak, izin/kapalı gün, randevu kümesi, durum kuralları, çakışma, müsaitlik; tekrar kuralı ve bekleme listesi (Faz 2) |
| Vernuko.Packages | Paket tanımı, müşteri paketi, append-only seans defteri |
| Vernuko.Payments | Append-only cari hesap, bakiye/kasa hesabı, ters kayıt; prim kuralları ve append-only prim defteri (Faz 2) |
| Vernuko.Inventory | Ürün, append-only stok hareketleri, hizmet malzemeleri (Faz 2) |
| Vernuko.Licensing | İmzalı lisans belgesi, ECDSA doğrulama, çevrimdışı durum değerlendirme |
| Vernuko.Messaging | wa.me bağlantısı, şablon motoru (hassas profilde hizmet adı gizleme), SMS sağlayıcı arayüzü |
| Vernuko.Reporting | Günlük özet, iptal/gelmeme oranları |
| Vernuko.Backups | Kurtarma anahtarı, STREAM AES-GCM, arşiv + manifest, GFS saklama, yerel hedef |
| Vernuko.Persistence | `VernukoDbContext`, yapılandırmalar, dönüştürücüler, migration'lar, tetikleyiciler, güvenli başlatıcı, anlık görüntü |
| Vernuko.Application | Kullanım senaryoları (kurulum, kimlik, müşteri, katalog, randevu, paket, ödeme, rapor, ayar, mesaj, yedek, lisans, denetim, demo) |
| Vernuko.Infrastructure | Windows'a özgü uygulamalar (DPAPI, MachineGuid özeti), veri yolları, günlük, DI bağlama |
| Vernuko.Presentation | View model'ler, navigasyon, diyalog/platform arayüzleri |
| Vernuko.Desktop | WPF görünümler, tasarım sistemi, pencere, kısayollar, otomatik kilit, smoke test, Velopack giriş noktası ve güncelleme servisi |
| Vernuko.Tests | Domain, entegrasyon (gerçek SQLite), view model ve statik XAML kontrolleri |

**Talimattaki listeden sapmalar:** `Vernuko.Packages` ayrı modül olarak eklendi (paketler ödeme değildir; seans defteri kendi kurallarına sahip). `Vernuko.Presentation` eklendi (view model'lerin Linux CI'da test edilebilmesi için).

## Uygulama katmanı kuralları
- Her kullanım senaryosu **kısa ömürlü bir DbContext** açar (masaüstü için önerilen desen; uzun ömürlü context bellek ve eskime sorunları yaratır).
- **Okuma:** izin kontrolü → sorgu. Hassas alanlara yetkisiz erişim denemesi denetim günlüğüne yazılır.
- **Yazma:** izin kontrolü → lisans kapısı (salt-okunur modda engel; yedek/geri yükleme/dışa aktarma/parola hariç) → `BEGIN IMMEDIATE` → iş kuralı → denetim kaydı → kaydet → commit. Veritabanı koruma hataları (çakışma tetikleyicisi, benzersizlik, eşzamanlılık) kullanıcı dostu hatalara çevrilir.
- İş kuralı ihlalleri **istisna değil `Result`** ile döner (kod + Türkçe mesaj); beklenmeyen hatalar günlüğe yazılır, kullanıcıya genel mesaj gösterilir.

## Randevu motoru
- Bir randevu = **personel + kaynak(lar)** için `AppointmentBookings` satırları. Blok aralığı = [başlangıç − hazırlık, bitiş + temizlik).
- Doğrulama sırası: aktif personel/kaynak → çakışma (engel) → personel izni / kaynak bakımı (engel) → işletme kapalı günü (onay) → işletme/personel çalışma saati (onay) → personel-hizmet uyumu (onay) → gerekli kaynak türü seçilmemiş (onay) → geçmiş saat (onay) → paket uygunluğu (engel) / paket hakkı yetersiz (onay).
- "Onay gerektiren" sorunlar kullanıcıya listelenir; açık onayla kaydedilir ve onaylanan uyarılar denetim kaydına yazılır.
- Durum yan etkileri tek işlemde: tamamlandı → seans düş (paket) veya hizmet bedelini borçlandır; tamamlama geri alındı → seans iade / borç ters kaydı; gelmedi → (politika açıksa) seans düş; gelmedi düzeltildi → iade. Rezervasyonu yeniden aktifleştiren geçişler (iptali geri al, gelmedi → geldi) çakışmayı yeniden kontrol eder.

## Çalışma modelleri
| Model | Durum | Mimari |
|---|---|---|
| A — Tek bilgisayar | Faz 1 ✅ | WPF + yerel SQLite, internet gerekmez |
| B — Yerel ağ | Faz 7 ✅ | **Karar (2026-11-12): PostgreSQL yok.** Ana bilgisayardaki Vernuko gömülü Kestrel ile servis arayüzlerini yerel ağda sunar; veritabanı ana bilgisayardaki SQLite. İstemciler arayüzleri ağ vekiliyle kullanır. SQLite dosyası paylaşılmaz. Çakışma garantisi aynı (IMMEDIATE işlem + veritabanı tetikleyicileri). Ayrıntı: `MULTI_PC.md`. |
| C — Bulut / çok şube | P2 | Kiracı başına veri izolasyonu, şube bazlı yetki/rapor, merkezi kullanıcı yönetimi. GUID v7 ve `BranchId` sayesinde yeniden anahtarlama gerekmez. |

**Bulut yedekleme ≠ canlı senkronizasyon.** Yedekleme (Faz 4) şifreli dosya kopyalar; çok cihazlı canlı çalışma (Faz 7) merkezi veritabanı gerektirir. İkisi aynı mekanizma olarak tasarlanmamıştır.

## Veri yolları (Windows)
`%LOCALAPPDATA%\Vernuko\Data\` → `vernuko.db`, `safety\` (migration/geri yükleme öncesi kopyalar), `temp\` (açılışta temizlenir), `logs\`. Demo: `%LOCALAPPDATA%\Vernuko\Demo\`. Varsayılan yedek klasörü: `Belgeler\Vernuko Yedekler` (harici disk önerilir).

## Komut satırı
`Vernuko.exe` · `Vernuko.exe --demo` (ayrı demo veritabanı, örnek veriler) · `Vernuko.exe --smoke-test <klasör>` (geçici veritabanı, tüm ekranların görüntüsü, bağlama hatası raporu, çıkış kodu).
