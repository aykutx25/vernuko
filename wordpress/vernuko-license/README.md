# Vernuko Lisans — WordPress eklentisi

Vernuko masaüstü uygulamasının lisans sunucusu, WordPress + WooCommerce sitenizin içinde. Ayrı sunucu (VPS) gerekmez.

**Ne yapar:**
- WooCommerce'te satılan ürün → ödeme tamamlanınca lisans anahtarı (`VRNK-…`). Anahtar müşteriye siparişin e-postasında, sipariş sayfasında ve **Hesabım › Lisanslarım**'da gösterilir.
- Masaüstü uygulama anahtarla etkinleşir. Haftada bir kendiliğinden yenilenir. İnternet yoksa 14 gün daha çalışır, sonra salt-okunur olur; **veriler asla silinmez**.
- Cihaz sınırı vardır. Bilgisayar değiştirmek için programda "Bu bilgisayardan kaldır" yapılır; bozulan bilgisayar müşteri tarafından Hesabım'dan, ya da yönetici tarafından kaldırılır.
- Yenileme: Hesabım › Lisanslarım › **Yenile** (ayrı bir "Yenileme" ürünü). Süre, mevcut dönem sonunun üstüne eklenir.
- İade/iptal edilen sipariş: yeni lisans hemen biter; yenileme siparişinde eklenen süre geri alınır.
- Sunucu tarafında deneme kaydı tutulur; program yeniden kurulsa da 30 günlük deneme baştan başlamaz.
- Güncelleme dosyaları yalnızca aboneliği süren kurulumlara verilir (isteğe bağlı).
- Müşterilerin randevu/müşteri verisi bu siteye **gelmez**. Gelenler yalnızca lisans anahtarı, özetlenmiş cihaz kimliği ve etkinleştirme kimliğidir.

Ödeme yöntemi WooCommerce'in kendisinindir: iyzico, PayTR ve havale/EFT eklentilerinin hepsiyle çalışır. Havalede siparişi "Tamamlandı" yaptığınızda lisans oluşur.

## Kurulum
1. `vernuko-license.zip` dosyasını yükleyin: Eklentiler › Yeni ekle › Eklenti yükle. Sonra etkinleştirin. ZIP, GitHub Actions'ta "vernuko-license-wordpress-plugin" adıyla üretilir; isterseniz `wordpress/vernuko-license` klasörünü `wp-content/plugins/` altına kopyalayın.
2. Ayarlar › Kalıcı bağlantılar: **"Yazı adı"** seçili olmalı. "Düz" seçiliyse REST adresleri çalışmaz.
3. Yönetim menüsünde **Vernuko Lisans** › **Anahtarları oluştur**. Gösterilen üç satırı `wp-config.php` dosyasına, `/* That's all, stop editing! */` satırının üstüne yapıştırın. Bir kopyasını parola yöneticisinde saklayın. **Bu anahtarlar kaybolursa satılmış lisanslar yenilenemez.** Anahtarlar veritabanına yazılmaz.
4. Sayfayı yenileyin. Ekranda iki değer görünür:
   - `VERNUKO_LICENSE_SERVER_URL` (ör. `https://siteniz.com/wp-json/vernuko/`)
   - `VERNUKO_LICENSE_KEYS`

   İkisini GitHub deposunda Settings › Secrets and variables › Actions › **Variables** bölümüne girin; ikisi de gizli değildir. Bundan sonra yayınlanan sürüm, lisansı bu siteden alır.
5. WooCommerce › Ürünler › Yeni:
   - **Satış ürünü:** sanal ürün. Genel sekmesinde "Vernuko lisansı" işaretli, Tür "Yeni lisans"; paket adı, bilgisayar sayısı ve süre girilir.
   - **Yenileme ürünü:** aynı ayarlar, Tür "Yenileme". Müşteri bu ürünü yalnızca Hesabım › Lisanslarım'daki **Yenile** düğmesiyle sepete ekleyebilir.
6. (İsteğe bağlı) Güncellemeleri siteden vermek için `wp-config.php` dosyasına web kökünün **dışındaki** bir klasörü yazın: `define('VERNUKO_LICENSE_RELEASES_DIR', '/home/kullanici/vernuko-releases');`. Velopack çıktısını bu klasöre koyun ve GitHub değişkenine `VERNUKO_UPDATE_URL=https://siteniz.com/wp-json/vernuko/v1/updates/feed` yazın.

## Yönetim
Vernuko Lisans sayfasından:
- elle lisans oluşturma (mağaza dışı satış),
- arama, anahtarı yeniden gösterme (denetim kaydına yazılır),
- +365 gün, iptal (ödenmiş dönem sonuna kadar çalışır),
- bilgisayar kaldırma.

## Güvenlik
- Özel imzalama anahtarı yalnızca `wp-config.php` dosyasındadır. Masaüstü uygulamada yalnızca açık anahtar bulunur; sahte lisans üretilemez.
- Lisans anahtarı veritabanında saklanmaz, yalnızca SHA-256 özeti tutulur. Anahtar, lisans kimliği ve sırdan yeniden türetilir.
- Uç noktalar IP başına dakikada 30 istekle sınırlıdır. Yönetim işlemleri WordPress yetkisi (`manage_woocommerce`) ve nonce ister.
- Hazır "lisans yöneticisi" eklentilerinin aksine, mağazanızın API anahtarı programa konmaz.

## Neler denendi
- Çekirdek: `php tests/core-test.php` (33 kontrol).
- Masaüstü istemcisiyle PHP sunucusu üzerinden etkinleştirme, yenileme, taşıma ve deneme kaydı: `WordPressPluginTests` (.NET).
- Gerçek WordPress 6.x + WooCommerce 11.3 (SQLite) üzerinde (2026-11-13):
  - Kartla ödemede anahtar "Sipariş alındı" e-postasında; havalede "Tamamlandı" e-postasında.
  - Yöneticiye giden e-postada anahtar yok.
  - Aynı sipariş iki kez işlense de tek lisans oluşuyor.
  - Yenileme ve iade akışları; başkasının lisansı yenilenemiyor.
  - Hesabım sayfası ve yönetici ekranı.
  - Masaüstü istemcisi gerçek siteden etkinleştirme, yenileme ve kaldırma yaptı.
  - İstek sınırı (30. istekten sonra 429).

**Denenmedi:**
- MySQL/MariaDB üzerinde çalışan gerçek bir barındırma hizmeti.
- Gerçek iyzico/PayTR ödemesi.
- Gerçek e-posta gönderimi (e-postaların içeriği yakalanıp kontrol edildi).
- WooCommerce Subscriptions (otomatik yinelenen ödeme) desteği yok; yenileme elle satın alınır.
