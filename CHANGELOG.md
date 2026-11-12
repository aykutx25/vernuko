# Değişiklik Günlüğü

Biçim: [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/). Sürümleme: [SemVer](https://semver.org/lang/tr/).

## [0.1.0] — 2026-11-12 — Faz 0 + Faz 1 çekirdeği (geliştirme sürümü)

### Eklendi
- **Faz 0 belgeleri:** kaynaklı rakip ve pazar araştırması (12 uluslararası + 13 Türk ürün), meslek modül matrisi, veritabanı mimarisi, lisans/ödeme/imzalama kararı, yedekleme ve mesajlaşma mimarisi, tasarım sistemi, P0/P1/P2 iş listesi, güvenlik/KVKK, maliyet modeli, test planı.
- **.NET 10 LTS modüler monolit** (16 proje), merkezi paket yönetimi, analizörler hata seviyesinde, NuGet güvenlik denetimi.
- **Randevu motoru:** 10 durum ve geçiş kuralları, personel/oda/cihaz çakışma kontrolü (hazırlık–temizlik tamponlarıyla), müsait saat önerisi, çalışma saatleri, izin/tatil/kapalı gün, yeniden planlama, iptal/iptali geri alma, DST güvenli UTC saklama.
- **SQLite kalıcılık:** IMMEDIATE yazma işlemleri, WAL + synchronous=FULL, veritabanı seviyesinde çakışma tetikleyicisi, finans/seans/denetim tablolarında append-only tetikleyiciler, migration öncesi güvenlik kopyası ve hata halinde geri dönüş, yeni sürüm veritabanı koruması.
- **Müşteriler:** Türkçe arama, mükerrer uyarısı, arşiv, anonimleştirme, JSON/CSV dışa aktarım, T.C. kimlik sağlaması ve maskeleme, ayrı pazarlama izni.
- **Paket & seans defteri**, **cari hesap/kasa** (idempotent tahsilat, ters kayıtla düzeltme).
- **Güvenlik:** 7 rol, 28 işlem izni, PBKDF2 (600k), hesap kilidi, ekran kilidi, hash zincirli denetim günlüğü, yetkisiz erişim kaydı.
- **Şifreli yedekleme:** AES-256-GCM STREAM, kurtarma anahtarı (HKDF), DPAPI ile yerel anahtar, doğrulama, geri yükleme, GFS saklama.
- **Lisans istemcisi:** ECDSA P-256 imzalı belge, çevrimdışı tolerans, saat geri alma koruması, salt-okunur mod.
- **WPF uygulaması:** özel başlık çubuğu, kurulum sihirbazı, giriş/kilit, ana sayfa, randevular (gün/hafta/ay + hızlı randevu paneli), müşteriler, hizmetler/odalar/paket tanımları, personel/izinler, paket & seanslar, ödemeler, raporlar, ayarlar (işletme, kullanıcılar, yedekleme, entegrasyonlar ve maliyetler, lisans, denetim, hesabım); kendi ikon seti ve tasarım tokenları; demo modu; smoke test modu.
- **Testler:** 120 test (119 Linux'ta geçer, 1 yalnızca Windows'ta çalışır): domain, gerçek SQLite entegrasyon, eşzamanlılık, view model, statik XAML kontrolleri, başarı kriterleri uçtan uca.
- **CI:** Linux (derleme + test) ve Windows (derleme + test + yayın + smoke test + ekran görüntüsü artefaktı).

### Düzeltildi
- Aynı dakika içinde alınan iki yedeğin dosya adının çakışması (GUID v7'nin zaman ön eki kullanılıyordu; artık rastgele kısım kullanılıyor). Entegrasyon testi sırasında bulundu.

### Bilinen sınırlar
- Arayüz Windows'ta çalıştırılarak görsel doğrulanmadı (Windows CI sonucu bekleniyor).
- Otomatik SMS/WhatsApp, bulut yedek, lisans sunucusu, kurulum paketi ve diğer mesleklerin özel modülleri henüz yok.
