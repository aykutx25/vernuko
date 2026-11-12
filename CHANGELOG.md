# Değişiklik Günlüğü

Biçim: [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/). Sürümleme: [SemVer](https://semver.org/lang/tr/).

## [0.2.3] — 2026-11-12 — Görünüm düzeltmeleri (Windows geri bildirimi)

### Düzeltildi
- **Tarih kutuları** Windows'un eski varsayılan görünümündeydi (yazı üstte, kutu uzun, eski takvim simgesi). Diğer kutularla aynı yükseklik, köşe ve odak rengine sahip yeni tasarım.
- **Arama çubuğu:** "Ctrl + K" etiketi kutunun tüm yüksekliğini kaplıyordu; ortalandı.
- **Bekleme listesine müşteri eklenemiyordu:** ad yazılıp listeden seçilmediğinde yalnızca "Müşteri seçin" deniyordu. Artık yazılan ad tek bir müşteriyle eşleşiyorsa otomatik seçilir; kayıtlı değilse ya da birden fazla eşleşme varsa ne yapılacağı açıkça yazılır. Aynı düzeltme stok ekranındaki ürün satışında da var.
- **Bekleme listesi tablosunda "Müşteri" sütunu görünmüyordu** (sabit genişlikli sütunlar yer bırakmayınca sıfıra düşüyordu). Tablo 1366 px ekrana sığacak şekilde yeniden düzenlendi; tüm tablolarda sütunların sıfıra düşmesi engellendi.
- Sayfa kaydırma alanlarında beliren kesik çizgili odak çerçevesi kaldırıldı.

## [0.2.2] — 2026-11-12 — Windows'ta doğrulama ve düzeltmeler

### Düzeltildi
- **Açılır kutular seçili değeri "ServiceDto { Id = … }" gibi iç veri olarak gösteriyordu** (hizmet, personel ve diğer tüm seçim kutuları). Tasarım şablonu düzeltildi; aynı hatayı yakalayan statik test eklendi.
- **Klavye kısayolları (Ctrl+N, Ctrl+L, Ctrl+S, Esc, F5, Alt+←/→, Alt+T) imleç bir alanın içinde değilken çalışmıyordu** (ör. girişten hemen sonra). Kısayollar artık pencere genelinde işler; açık bir diyalog varken yalnızca Esc kabul edilir.

### Doğrulandı
- Gerçek bir Windows bilgisayarda: giriş, yeni randevu paneli (Ctrl+N), açılır kutular elle denendi; smoke test 11 sayfa + 20 sekme, **0 bağlama hatası**, parola kutusu kontrolü başarılı.

## [0.2.1] — 2026-11-12 — İlk Windows çalıştırmasından düzeltmeler

### Düzeltildi
- **Parola kutuları yazılan parolayı programa iletmiyordu** (giriş, ekran kilidi, kurulum sihirbazı, parola değiştirme). Her giriş boş parola olarak değerlendiriliyor ve hesap 5 denemeden sonra kilitleniyordu. Gerçek bir Windows bilgisayarda ilk çalıştırmada bulundu.
- Boş kullanıcı adı/parola artık "Kullanıcı adı ve parolayı girin" uyarısı verir ve hesap kilidine sayılmaz.
- Smoke test, ekranları çizmeden önce parola kutusu bağlamasını iki yönde de denetler.

## [0.2.0] — 2026-11-12 — Faz 2 (Güzellik & Lazer operasyonları) + kurulum paketi (geliştirme sürümü)

### Eklendi
- **Prim / komisyon:** hizmet, paket satışı ve ürün satışı için kurallar (yüzde veya sabit tutar; personel + hizmet > personel > hizmet > genel). Satan ve uygulayan ayrı primlenir. Primler işlem anında hesaplanır, değiştirilemez (append-only); randevu geri alınınca ters kayıt. Ayarlar › Prim kuralları, Raporlar › Personel primleri (yalnızca finansal rapor yetkisiyle).
- **Stok:** ürünler, stok girişi, gerekçeli sayım düzeltmesi, müşteriye ürün satışı (hemen tahsil veya borç), hizmet tamamlanınca otomatik malzeme düşümü ve geri almada iade, kritik stok uyarısı. Stok hareketleri append-only.
- **Tekrarlayan randevu:** gün/hafta/ay aralıkla 2–52 tekrar; yerel saat korunur (yaz saati), ay sonu kuralı; çakışan tarihler kaydetmeden önce listelenir ve açık onayla atlanır. Seriyi "bu randevudan itibaren" taşıma veya iptal etme.
- **Bekleme listesi:** müşteri, hizmet/personel tercihi, tarih aralığı; bir randevu iptal edilince uygun bekleyenler gösterilir (mesaj otomatik gitmez).
- **Onam formları:** sürümlü metinler (Ayarlar › Onam formları), müşteri kartında onay kaydı (kâğıt/sözlü/ekranda), gerekçeli geri alma. Hukuki metin sağlanmaz.
- **Şifreli müşteri dosyaları:** fotoğraf, imzalı onam taraması, belge; dosya başına AES-256-GCM, önizleme yalnızca bellekte, her görüntüleme denetim kaydında, ayrı yetki (sekreter göremez), yedeğe dahil ve yeni bilgisayarda kurtarma anahtarıyla açılır, anonimleştirmede silinir.
- **Kurulum paketi ve güncelleme (Velopack):** kullanıcı başına kurulum (yönetici yetkisi yok), Ayarlar › Güncellemeler (yalnızca tıklayınca denetler/indirir, önce şifreli yedek alır), HTTPS'e sabitlenmiş güncelleme kaynağı, CI'da deneme kurulum paketi, etiketle taslak GitHub sürümü. Bkz. [docs/RELEASE.md](docs/RELEASE.md).
- 6 yeni izin (toplam 34): stok görüntüleme/yönetme, ürün satışı, onam kaydı, dosya görüntüleme/yönetme.
- Smoke test artık her sayfanın her sekmesini çizer.
- 16 yeni test (toplam 136).

### Değişti
- Yedek doğrulama mesajı müşteri dosyalarının yedeğe dahil olduğunu belirtir (önceki "fotoğraf içermez" metni artık doğru değildi).
- Veri klasörü adı `AppIdentity` sabitine taşındı; kurulum klasörü (`VernukoApp`) ile çakışmadığı testle güvence altında.

### Bilinen sınırlar
- Faz 2 ekranları ve kurulum paketi Windows'ta çalıştırılarak doğrulanmadı (Windows makinesi yok; Wine .NET 10'u çalıştıramıyor; CI sonuçlarına bu oturumdan erişilemedi).
- Yükleyici imzasız (SmartScreen uyarısı verir); sertifika alınınca imzalanacak.
- Otomatik SMS/WhatsApp, bulut yedek, lisans sunucusu ve diğer mesleklerin özel modülleri henüz yok.

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
