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

## Logo

Vernuko logosu (iki yaprak + "Vernuko" yazısı) markanın verdiği görselden vektöre çevrildi; her boyutta keskin.

| Dosya | Ne | Nerede |
|---|---|---|
| `assets/img/vernuko-logo.svg` | İşaret + yazı | Üst ve alt bölüm |
| `assets/img/vernuko-yazi.svg` | Yalnızca yazı | Program adlarında: "Vernuko Randevu", "Vernuko Stok", "Vernuko Teknik Servis" |
| `assets/img/vernuko-isaret.svg` | Yalnızca iki yaprak | Kaynak dosya |
| `assets/img/favicon.svg` | Tarayıcı sekmesi simgesi | Koyu temalı sekmede beyaza döner |
| `assets/img/apple-touch-icon.png` | 180 px, lacivert zemin üzerinde beyaz yapraklar | iPhone/iPad ana ekranı |

- **Renk:** logonun kendi laciverti `#041538`. Koyu zeminde beyaz kullanılır; başka renk verilmez.
- **Program adı:** Vernuko yazısının yanına programın adı, sitenin yazı tipiyle ve aynı büyük harf yüksekliğinde yazılır. Bunu `vernuko/urun-logo` bloğu yapar; ana sayfada ve program sayfalarında başlığın üstünde durur. Program listesinde de aynı biçim kullanılır.
- **Site simgesi:** WordPress'te **Görünüm › Özelleştir › Site kimliği** ile bir simge yüklerseniz o kullanılır; yüklemezseniz tema yapraklı simgeyi verir.

## Vernuko programları (1.1.0, metinler 1.3.0)

Tema üç programı tanır: Vernuko Randevu, Vernuko Stok ve Vernuko Teknik Servis. Üçü masaüstünde tek kurulum dosyasıdır (masaüstü 0.16.0); kurulumda seçilen işletme türü programı belirler. Hangi programın "Satışta" görüneceğini eklentideki paketler belirler.

- **Ana sayfa:** "Vernuko programları" bölümü hepsini tek bir defter satırı gibi listeler: ne tuttuğu, kimin için, durumu.
- **Program sayfaları:** `/stok/` ve `/teknik-servis/`. Her birinde şunlar var:
  - programın kendi gün ekranı: hırdavat, kırtasiye, yedek parça; telefon, beyaz eşya, bilgisayar servisi,
  - programdaki özellikler (Stok sayfasında "mali fiş kesmez" notuyla),
  - satış bölümü,
  - diğer programlar.
- **Durum kendiliğinden değişir.** Bir program, eklentide ona ait fiyatı olan bir "Yeni lisans" paketi tanımlandığı an "Satışta" olur. O andan itibaren:
  - program sayfasındaki "geliştiriliyor" notu yerine fiyat fişi ve indirme düğmesi çıkar,
  - ana sayfadaki liste "Satışta" yazar.

  Sayfaları elle değiştirmek gerekmez.
- **Dürüstlük:**
  - Özellik listeleri yalnızca programda olanı yazar (1.3.0'da masaüstü 0.16.0'a göre güncellendi).
  - Ekran görüntüsü yerine kullanılan gün ekranı "temsilî ekran" olarak etiketlidir.
  - Satışta olmayan program için tarih verilmez, e-posta toplanmaz.
- **Bloklar:** düzenleyicide her bloğun ayarında program seçilir.
  - `vernuko/pano`: ekran (randevu, stok veya servis).
  - `vernuko/fis`: program.
  - `vernuko/urun-satis`: program.
  - `vernuko/urunler`: listede gösterilmeyecek program.
- **Kısa kodlar:** `[vernuko_pano ekran="stok"]`, `[vernuko_fis urun="vernuko-stok"]`, `[vernuko_urunler haric="vernuko-desktop"]`.

**Yeni bir programı satışa çıkarmak:**
1. Eklentide **Paketler ve satış** sayfasında programı seçerek "Yeni lisans" ve "Yenileme" paketlerini girin.
2. Kurulum dosyasını `VERNUKO_LICENSE_RELEASES_DIR/<program kimliği>/` klasörüne koyun. Örnek: `vernuko-stok/`. Üç program aynı kurulum dosyasını kullanır; aynı dosya her programın klasörüne konur.
3. Program sayfasındaki "Programda neler var" listesini yeni sürümün özellikleriyle karşılaştırın. Gün ekranının altyazısı `inc/pano.php` dosyasındadır.

Dördüncü bir program için:
- tema tarafında `vernuko_tema_urunler` süzgeci (ad, sayfa, özet, kimler için, fiş satırları),
- eklenti tarafında `vernuko_license_products` süzgeci,
- program sayfası için yeni bir desen.

## Gerekenler

- WordPress 6.6 veya üstü (WordPress 7.1.4'te denendi), PHP 8.1 veya üstü.
- Vernuko Lisans eklentisi 1.4.0 veya üstü (programlar için; 1.3.0 ile yalnızca Randevu çalışır). Eklenti yoksa tema yine açılır; fiyat fişinin yerinde yöneticiye bir uyarı görünür.

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

   Program sayfaları için şablon olarak **Geniş sayfa (program sayfaları)** seçin:

   | Adres | Başlık | Desen |
   |---|---|---|
   | `/stok/` | Vernuko Stok | Program sayfası: Vernuko Stok |
   | `/teknik-servis/` | Vernuko Teknik Servis | Program sayfası: Vernuko Teknik Servis |

   Üst menü ve alt bölüm bu adreslere bağlantı verir. Adresleri değiştirirseniz bağlantıları **Görünüm › Düzenleyici** içinden güncelleyin.
5. **Vernuko Lisans › Paketler ve satış** sayfasında şunları yapın:
   - En az bir "Yeni lisans" paketi tanımlayın ve programını seçin. Fiyat KDV dahil girilir; fiş "Toplam, KDV dahil" yazar.
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
| `templates/`, `parts/` | Ana sayfa, sayfa, sade sayfa, geniş sayfa (program sayfaları), yazı, arşiv, arama, 404, WooCommerce ödeme sayfası; üst, alt ve ödeme sayfası üst bölümü |
| `patterns/` | Ana sayfanın bölümleri, satın alma/indirme/Lisanslarım sayfaları, yasal taslaklar |
| `inc/urunler.php` | Programlar: tanıtım bilgileri, satış durumu, program logosu; `vernuko/urunler`, `vernuko/urun-satis`, `vernuko/urun-logo` blokları |
| `inc/pano.php` | Gün ekranları (Randevu, Stok, Teknik Servis): `vernuko/pano` bloğu ve `[vernuko_pano]` kısa kodu |
| `inc/fis.php` | Fiyat fişi (programa göre): `vernuko/fis` bloğu ve `[vernuko_fis]` kısa kodu |
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

**1.1.0 (2026-11-14):** aynı sitede, eklenti 1.4.0 ile denendi.
- Stok ve Teknik Servis sayfaları masaüstünde ve telefonda açıldı. Dış istek, konsol hatası ve yatay kaydırma yok.
- Stok ve Servis gün ekranlarında internet kapatılınca "Kasadan satış" ve "Kapıdan gelen cihaz" satırları ekleniyor.
- **Satışa çıkış:** Stok paketleri ve kurulum dosyası eklenince Stok sayfası kendiliğinden satış bölümüne geçti. Fiş "Satış başına komisyon" ve "Ürün ve cari sınırı" satırlarıyla göründü. Ana sayfa listesi "Satışta" yazdı.
- Fişten satın alma sayfasına gidildi, sahte iyzico ile ödendi, Stok anahtarı alındı.
- Blok düzenleyicide program sayfası hatasız açıldı; bloklar önizlemeyle görünüyor.

**1.3.1 (2026-11-17):** WooCommerce 11.3 ödeme sayfası temanın logosuyla açılıyor (`templates/page-checkout.html`, `parts/checkout-header.html`). Önceden WooCommerce'in kendi üst bölümü düz "Vernuko" yazısı gösteriyordu. Mağaza, ürün, sepet ve ödeme sayfaları 1440 ve 390 pikselde denendi; konsol hatası ve yatay kaydırma yok.
- Sürüm numarası her desen eklenişinde artırılmalı: WordPress temanın desen listesini sürüme göre önbellekte tutar; sürüm aynı kalırsa yeni desen görünmez.

**Denenmedi:**
- MySQL/MariaDB üzerinde gerçek bir barındırma hizmeti.
- Firefox ve Safari.
- Ekran okuyucuyla tam bir gezinti.
- Site düzenleyicide (Görünüm › Düzenleyici) desenlerin düzenlenip kaydedilmesi.
