# Vernuko satış teması

Vernuko'nun satış sitesi için WordPress blok teması. Vernuko Lisans eklentisiyle birlikte çalışır; satın alma, Lisanslarım ve indirme o eklentidedir.

Tasarımın nereden geldiği (pazar araştırması, renkler, yazı tipi, neden böyle) için: [DESIGN.md](DESIGN.md).

## Sayfada ne var

- **Ana sayfa:**
  - Programın gün ekranı canlı çizilir. Ziyaretçi işletme türünü (kuaför, diş kliniği, avukat…) değiştirebilir. İnterneti kapatınca, kapıdan gelen müşterinin yine de kaydedildiğini görür.
  - "Kayıtlarınız nerede durur?" şeridi, saat saat bir iş günü, işletme türleri tablosu, fiyat fişi, sorular.
- **Fiyat fişi:** Fiyatlar eklentideki paketlerden okunur; temaya fiyat yazılmaz. Kurulum, kullanıcı başı ücret ve komisyon gibi kalemler "0,00 TL" olarak açıkça yazılır. Paket seçince fiş ve "Bu paketi satın al" bağlantısı değişir.
- **Gizlilik:**
  - Tema hiçbir dış sunucudan bir şey yüklemez: yazı tipi temanın içinde, emoji betiği ve Gravatar kapalı, analiz ve reklam kodu yok.
  - Sitenin vaadi programın vaadiyle aynıdır: ziyaretçinin bilgisi üçüncü bir şirkete gitmez.
- **Yazı tipi:** Schibsted Grotesk, temanın içinde. SIL Open Font License 1.1: `assets/fonts/OFL.txt`.

## Gerekenler

- WordPress 6.6 veya üstü (WordPress 7.1.4'te denendi), PHP 8.1 veya üstü.
- Vernuko Lisans eklentisi 1.3.0 veya üstü. Eklenti yoksa tema yine açılır; fiyat fişinin yerinde yöneticiye bir uyarı görünür.

## Kurulum

1. Tema dosyasını edinin:
   - CI her derlemede `vernuko-theme-wordpress` adında bir zip üretir.
   - Ya da `wordpress/vernuko-theme` klasörünü kendiniz zipleyin.
2. **Görünüm › Temalar › Yeni ekle › Tema yükle** ile zip'i yükleyip etkinleştirin.
3. Ana sayfa için ayrıca bir sayfa gerekmez: tema ana sayfayı kendi şablonuyla gösterir.
4. Aşağıdaki sayfaları oluşturun. Her birinde:
   - Şablon olarak **Sade sayfa** seçin.
   - Sayfaya blok ekleyicisinin **Desenler › Vernuko** bölümündeki deseni ekleyin.

   | Adres | Başlık | Desen |
   |---|---|---|
   | `/indir/` | İndir | Sayfa: indir |
   | `/satin-al/` | Satın al | Sayfa: satın al |
   | `/lisanslarim/` | Lisanslarım | Sayfa: Lisanslarım |
   | `/on-bilgilendirme-formu/` | Ön bilgilendirme formu | Yasal taslak: ön bilgilendirme / mesafeli satış sözleşmesi |
   | `/mesafeli-satis-sozlesmesi/` | Mesafeli satış sözleşmesi | Yasal taslak: ön bilgilendirme / mesafeli satış sözleşmesi |
   | `/kvkk-aydinlatma-metni/` | KVKK aydınlatma metni | Yasal taslak: KVKK aydınlatma metni (satış sitesi) |
   | `/cerez-politikasi/` | Çerez politikası | Yasal taslak: çerez politikası |

   Üst menü ve alt bölüm bu adreslere bağlantı verir. Adresleri değiştirirseniz bağlantıları **Görünüm › Düzenleyici** içinden güncelleyin.
5. **Vernuko Lisans › Paketler ve satış** sayfasında şunları yapın:
   - En az bir "Yeni lisans" paketi tanımlayın. Fiyat KDV dahil girilir; fiş "Toplam, KDV dahil" yazar.
   - **Satın alma sayfası:** `/satin-al/` adresini girin. Fişteki düğme buraya `?paket=…` ile gider.
   - **Lisanslarım**, **ön bilgilendirme** ve **KVKK** sayfalarının adreslerini girin. Satın alma formundaki onay kutuları bunlara bağlanır.
6. İndirme düğmesi için `wp-config.php` dosyasında `VERNUKO_LICENSE_RELEASES_DIR` sabitini tanımlayın.
   - Değeri, Velopack çıktısının durduğu klasördür. Web kökünün dışında olmalıdır.
   - En yeni `…-Setup.exe` dosyası `/wp-json/vernuko/v1/indir` adresinden indirilir.
   - Sürüm numarası `releases.win.json` dosyasından okunur.

## Yayına almadan önce yapılması gerekenler

- **Satıcı bilgileri:** Alt bölümdeki köşeli parantezli alanları doldurun: ticari unvan, adres, vergi dairesi ve numarası, MERSİS, e-posta, telefon. Bunlar **Görünüm › Düzenleyici › Desenler › Şablon parçaları › Alt** bölümünde düzenlenir.
- **Yasal metinler taslaktır.** Ön bilgilendirme formu, mesafeli satış sözleşmesi, KVKK aydınlatma metni ve çerez politikası:
  - Bu desenler yalnızca başlık iskeleti ve örnek içerik verir.
  - Yayından önce hukuk danışmanınız yazmalı ya da onaylamalıdır.
  - Lisans dijital içerik olarak anında teslim edilir. Bunun cayma hakkına etkisi ayrıca değerlendirilmelidir.
- **Kurulum dosyası imzalanmalıdır.**
  - İmzasız `Setup.exe` iki sorun çıkarır: Windows SmartScreen "tanınmayan yayıncı" uyarısı gösterir; Windows 11'de Akıllı Uygulama Denetimi açıksa program hiç çalışmaz.
  - Satıştan önce kod imzalama sertifikası alınmalı ve kurulum dosyası imzalanmalıdır. Seçenekler için: [docs/RELEASE.md](../../docs/RELEASE.md).
  - Sitede ya da destekte müşteriye SmartScreen'i, Defender'ı veya Akıllı Uygulama Denetimi'ni kapatması söylenmez.
- **Gün ekranındaki işletmeler ve kişiler hayal ürünüdür.** Ekranın altında bu yazar. Verileri değiştirmek için: `inc/pano.php` (`vernuko_pano_meslekler`).
- **Siteye analiz, reklam veya canlı destek aracı eklerseniz:**
  - Alt bölümdeki "Bu site sizi izlemez" notunu kaldırın.
  - Çerez politikasını güncelleyin.
  - Gerekiyorsa açık rıza akışı ekleyin.

## Dosyalar

| Yol | Ne |
|---|---|
| `theme.json` | Renkler, yazı tipi, boyutlar, boşluklar, düğme ve bağlantı stilleri |
| `templates/`, `parts/` | Ana sayfa, sayfa, sade sayfa, yazı, arşiv, arama, 404; üst ve alt bölüm |
| `patterns/` | Ana sayfanın bölümleri, satın alma/indirme/Lisanslarım sayfaları, yasal taslaklar |
| `inc/pano.php` | Gün ekranı: `vernuko/pano` bloğu ve `[vernuko_pano]` kısa kodu |
| `inc/fis.php` | Fiyat fişi: `vernuko/fis` bloğu ve `[vernuko_fis]` kısa kodu |
| `assets/css/site.css` | Temanın tüm stilleri; eklentinin satın alma ve Lisanslarım çıktısına uyum dahil |
| `assets/js/` | Gün ekranı ve fişin küçük betikleri (bağımlılık yok), düzenleyicideki blok önizlemesi |

## Neler denendi (2026-11-14)

- **Ortam:** WordPress 7.1.4 (SQLite) ve Vernuko Lisans 1.3.0. Chromium'da 390, 601–1024, 1200 ve 1440 piksel genişliklerde ekran görüntüsüyle bakıldı.
- **Hiçbir sayfa dış sunucuya istek atmıyor.** Bakılan sayfalar: ana sayfa, satın alma, indirme, Lisanslarım, yasal sayfalar, arama, 404. Konsolda hata yok, yatay kaydırma yok.
- **Gün ekranı:**
  - İşletme türü değişiyor.
  - İnternet anahtarı fareyle ve klavyeyle (boşluk tuşu) çalışıyor.
  - İnternet kapalıyken eklenen randevu doğru saate yerleşiyor.
- **Fiş:**
  - Paket seçince kalemler, toplam ve satın alma bağlantısı değişiyor.
  - Bağlantı, satın alma sayfasında doğru paketin formunu açıyor.
- **İndirme:** Düğme sürüm ve boyutu gösteriyor; dosya indiriliyor (deneme dosyasıyla).
- **Telefon menüsü:** Açılıyor, bağlantıya basınca kapanıp ilgili bölüme gidiyor.

**Denenmedi:**
- MySQL/MariaDB üzerinde gerçek bir barındırma hizmeti.
- Firefox ve Safari.
- Ekran okuyucuyla tam bir gezinti.
- Site düzenleyicide (Görünüm › Düzenleyici) desenlerin düzenlenip kaydedilmesi.
