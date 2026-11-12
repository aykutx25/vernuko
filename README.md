# Vernuko

Windows için **çevrimdışı çalışan** randevu, sekreter ve işletme yönetim yazılımı. Türkiye öncelikli; güzellik ve lazer merkezlerinden kliniklere, kuaförlerden eğitim kurumlarına kadar farklı meslekler **tek bir randevu motorunu** kullanır.

> **Durum: Faz 0 ve Faz 1 tamamlandı, Faz 2 (Güzellik & Lazer operasyonları) eklendi, kurulum paketi hazır (v0.2.0, geliştirme sürümü — ticari kullanıma hazır değil).**
> İş kuralları, veritabanı, yedekleme, lisans istemcisi ve ekranların view model'leri gerçek SQLite dosyaları üzerinde 136 otomatik testle doğrulandı. WPF arayüzü derleniyor, tüm bağlamaları statik olarak kontrol ediliyor ve kurulum paketi üretilebiliyor, **ancak henüz Windows'ta çalıştırılarak görsel olarak doğrulanmadı** (bkz. "Bilinen sınırlar").

## Ne çalışıyor (Faz 1)
- **Kurulum sihirbazı** (10 adım, atlanabilir adımlar), 14 meslek profili; "Güzellik ve Lazer Merkezi" tek profil.
- **Randevu motoru:** gün/hafta/ay, personel/oda/hizmet filtreleri, hazırlık–temizlik aralıkları, personel + oda + cihaz çakışma kontrolü (uygulama + eşzamanlı işlem + veritabanı tetikleyicisi), müsait saat önerisi, 10 randevu durumu ve kuralları, erteleme, iptal/iptali geri alma, izin ve kapalı günler, yaz saati güvenli zaman yönetimi.
- **Hızlı randevu paneli:** takvimden ayrılmadan müşteri arama/ekleme, hizmet, personel, oda, uygun saat çipleri, paket seçimi; klavye kısayolları.
- **Müşteriler:** Türkçe uyumlu arama (ı/İ/ş/ğ…), mükerrer kayıt uyarısı (otomatik birleştirme yok), arşiv, KVKK için tam JSON dışa aktarım ve anonimleştirme, CSV liste.
- **Paket & seans:** satış, kalan hak, bölge, tamamlanınca düşme, iptalde korunma, geri alma, gerekçeli düzeltme, dondurma/uzatma, geçmiş.
- **Ödemeler:** tahsilat, kapora, iade, indirim, ters kayıtla düzeltme (silme yok), çift tıklamaya dayanıklı kayıt, günlük kasa, açık bakiyeler. *Resmî fatura düzenlemez.*
- **Güvenlik:** kullanıcılar, 7 rol, 34 işlem izni, PBKDF2 parola, hesap ve ekran kilidi, hash zincirli denetim günlüğü.
- **Şifreli yedekleme:** AES-256-GCM, kurtarma anahtarı, doğrulama, geri yükleme (yeni bilgisayarda dahil), 7/4/3 saklama, zamanlanmış ve kapanışta yedek.
- **Lisans istemcisi:** ECDSA imzalı lisans belgesi, çevrimdışı tolerans, saat geri alma koruması; süre bitince **salt-okunur mod — veri asla silinmez**.
- **Manuel WhatsApp** (wa.me) hatırlatma; "Entegrasyonlar ve Maliyetler" ekranı (tüm ücretli entegrasyonlar kapalı, limit 0 ₺).
- **Demo modu** (`--demo`) ayrı veritabanında; **smoke test modu** (`--smoke-test`).

## Faz 2 ile eklenenler
- **Prim/komisyon:** hizmet, paket satışı, ürün satışı; satan ve uygulayan ayrı; en özel kural geçerli; değiştirilemez prim defteri; Raporlar › Personel primleri.
- **Stok:** ürünler, giriş, sayım düzeltmesi, ürün satışı, hizmet tamamlanınca otomatik malzeme düşümü, kritik stok.
- **Tekrarlayan randevu** (çakışan tarihler önceden listelenir), seriyi sonrakilerle birlikte taşıma/iptal.
- **Bekleme listesi:** iptalde uygun bekleyen müşteriler gösterilir.
- **Onam formları** (sürümlü) ve **şifreli müşteri fotoğrafları/belgeleri** (ayrı yetki, yedeğe dahil).
- **Kurulum paketi ve güncelleme** (Velopack): yönetici yetkisi istemez, güncellemeden önce şifreli yedek alır, kaldırma verileri silmez — [docs/RELEASE.md](docs/RELEASE.md).

## Henüz olmayanlar
Otomatik SMS/WhatsApp, Google Drive/OneDrive/Dropbox yedeği, lisans sunucusu ve satış, diğer mesleklerin özel modülleri, çoklu bilgisayar, yükleyici imzası. Plan: [docs/ROADMAP.md](docs/ROADMAP.md).

## Gereksinimler
- Çalıştırma: Windows 10 veya 11 (x64).
- Geliştirme: .NET SDK 10.0.1xx veya üzeri. Linux/macOS'ta tüm proje derlenir ve testler çalışır (WPF uygulaması yalnızca Windows'ta çalışır).

## Hızlı başlangıç
```bash
dotnet build Vernuko.sln
```
```bash
dotnet test tests/Vernuko.Tests/Vernuko.Tests.csproj
```
Windows'ta uygulamayı çalıştırma:
```bash
dotnet run --project src/Vernuko.Desktop
```
Örnek verilerle (gerçek verilerden tamamen ayrı demo veritabanı; giriş bilgisi giriş ekranında yazar):
```bash
dotnet run --project src/Vernuko.Desktop -- --demo
```
Tüm ekranların görüntüsünü alıp bağlama hatalarını denetleme (Windows):
```bash
dotnet run --project src/Vernuko.Desktop -- --smoke-test smoke-output
```
Veriler: `%LOCALAPPDATA%\Vernuko\Data` (demo: `%LOCALAPPDATA%\Vernuko\Demo`); müşteri dosyaları aynı klasörde `attachments\` altında şifreli. Kurulan program `%LOCALAPPDATA%\VernukoApp` altındadır; kaldırmak verileri silmez. Varsayılan yedek klasörü: `Belgeler\Vernuko Yedekler` — harici disk önerilir.

## Windows'ta çalıştırma (kaynak koddan)
- **`baslat.cmd`**: derler ve Vernuko'yu açar. **`demo.cmd`**: örnek verilerle (ayrı demo veritabanı) açar.
- Program Microsoft imzalı `dotnet` üzerinden başlatılır. İmzasız `Vernuko.exe` yeni Windows 11 bilgisayarlarda **Akıllı Uygulama Denetimi** (Smart App Control) tarafından "Device Guard policy" mesajıyla engellenir. Kod imzalama sertifikası alınana kadar kurulum paketi de bu bilgisayarlarda açılmaz (bkz. [docs/RELEASE.md](docs/RELEASE.md)).

## Yerel kontrol (Windows)
Depo kökündeki **`kontrol.cmd`** dosyasına çift tıklayın: güncel kodu indirir, derler, tüm testleri çalıştırır ve tüm ekranları açıp kontrol eder (smoke test). Sonunda "TÜM KONTROLLER BAŞARILI" ya da hata satırlarını gösterir; ekran görüntüleri `%USERPROFILE%\vernuko-kontrol` klasörüne kaydedilir. GitHub Actions şu an yalnızca elle başlatılır.

Kurulum dosyası üretmek için: **`kurulum-olustur.cmd`**.

## Klavye kısayolları
Ctrl+K arama · Ctrl+N yeni randevu · Ctrl+S kaydet · Ctrl+L kilitle · Esc kapat · F5 yenile · Alt+←/→ gün · Alt+T bugün.

## Proje yapısı
```
src/
  Vernuko.Core              ortak çekirdek (Result, para, telefon, Türkçe metin, saat dilimi)
  Vernuko.Security          kullanıcı, rol, izin, parola, denetim zinciri
  Vernuko.BusinessProfiles  meslek profilleri, işletme ayarları
  Vernuko.Customers         müşteri kartı
  Vernuko.Appointments      randevu motoru
  Vernuko.Packages          seans paketleri ve defter
  Vernuko.Payments          cari hesap / kasa, prim kuralları ve prim defteri
  Vernuko.Inventory         ürün, stok hareketleri, hizmet malzemeleri
  Vernuko.Licensing         lisans belgesi ve değerlendirme
  Vernuko.Messaging         wa.me, şablonlar, SMS arayüzü
  Vernuko.Reporting         özetler
  Vernuko.Backups           şifreli yedek biçimi, saklama, hedefler
  Vernuko.Persistence       EF Core + SQLite, migration, tetikleyiciler
  Vernuko.Application       kullanım senaryoları
  Vernuko.Infrastructure    DPAPI, cihaz kimliği, yollar, günlük
  Vernuko.Presentation      MVVM view model'ler
  Vernuko.Desktop           WPF uygulaması
tests/Vernuko.Tests         domain, entegrasyon, view model, XAML kontrolleri
docs/                       mimari, kararlar ve araştırma
```

## Belgeler
| Belge | İçerik |
|---|---|
| [docs/COMPETITOR_ANALYSIS.md](docs/COMPETITOR_ANALYSIS.md) | A. Rakip araştırması ve özellik matrisi ([kaynaklı raporlar](docs/research/)) |
| [docs/BUSINESS_PROFILES.md](docs/BUSINESS_PROFILES.md) | B. Meslek modül matrisi |
| [docs/DATABASE.md](docs/DATABASE.md) | C. Veritabanı mimarisi |
| [docs/LICENSING.md](docs/LICENSING.md) | D. Lisans altyapısı kararı (ve ödeme/imzalama) |
| [docs/BACKUP_RECOVERY.md](docs/BACKUP_RECOVERY.md) | E. Yedekleme ve kurtarma |
| [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md) | F. Mesajlaşma ve entegrasyonlar |
| [docs/UI_DESIGN.md](docs/UI_DESIGN.md) | G. Arayüz ve tasarım sistemi |
| [docs/ROADMAP.md](docs/ROADMAP.md) | H. P0/P1/P2 iş listesi, fazlar, riskler |
| [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) | Gereksinimler ve I. başarı kriterleri |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Mimari ve teknoloji kararları |
| [docs/SECURITY.md](docs/SECURITY.md) | Güvenlik ve KVKK |
| [docs/COST_MODEL.md](docs/COST_MODEL.md) | Maliyet modeli |
| [docs/TEST_PLAN.md](docs/TEST_PLAN.md) | Test planı ve 30 senaryonun karşılığı |
| [docs/RELEASE.md](docs/RELEASE.md) | Kurulum paketi, sürüm yayınlama, güncelleme, imzalama |
| [CHANGELOG.md](CHANGELOG.md) | Sürüm notları |

## Bilinen sınırlar
- **Arayüz Windows'ta smoke test ile doğrulandı** (11 sayfa, 20 sekme, 0 bağlama hatası; ilk çalıştırmada bulunan 3 hata düzeltildi). Başarı akışının elle baştan sona denenmesi ve kurulum paketinin Windows'ta kurulup kaldırılması henüz yapılmadı.
- Yükleyici imzasız; Windows SmartScreen uyarı verir. Sertifika alınınca imzalanacak (bkz. [docs/RELEASE.md](docs/RELEASE.md)).
- Yerel veritabanı dosyası şifreli değil (yedekler şifreli). BitLocker önerilir; veritabanı şifreleme P1.
- Performans hedefleri gerçek cihazlarda ölçülmedi.
- Hukuki konular (bulut yedek ve KVKK yurt dışı aktarım, İYS, sağlık verisi) avukat görüşü gerektirir — bkz. [docs/SECURITY.md](docs/SECURITY.md).
- **Stripe Türkiye'de kayıtlı işletmelere açık değil**; satış altyapısı için karar gerekiyor — bkz. [docs/LICENSING.md](docs/LICENSING.md).
