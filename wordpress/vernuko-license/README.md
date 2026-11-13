# Vernuko Lisans — WordPress eklentisi

Vernuko masaüstü uygulamasının lisans sunucusu, WordPress sitenizin içinde. Ayrı sunucu (VPS) gerekmez. Üç satış yolu var; birini ya da birkaçını birlikte kullanabilirsiniz:

| Yol | Ne zaman | Bölüm |
|---|---|---|
| **Kendi satış sayfası** (WooCommerce gerekmez) | Tek düğmeyle satın alma; ödeme iyzico ve/veya PayTR ile | [Kendi satış sayfası](#kendi-satış-sayfası-woocommerce-gerekmez) |
| **WooCommerce** | Sepet, kupon, hazır fatura eklentileri istiyorsanız | Aşağıdaki ilk bölümler |
| **Dış sistem** | Satış WordPress dışında bir yerde (kendi uygulamanız, başka bir mağaza) | [Dış satış sistemi](#dış-satış-sistemi) |

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

## Kendi satış sayfası (WooCommerce gerekmez)
1. `wp-config.php` dosyasına kullandığınız sağlayıcının bilgilerini ekleyin. Bilgiler sağlayıcının mağaza panelinde yazar; yalnızca bu dosyada durur.
   ```php
   // iyzico (Ortak Ödeme Sayfası)
   define('VERNUKO_IYZICO_API_KEY', '...');
   define('VERNUKO_IYZICO_SECRET_KEY', '...');
   define('VERNUKO_IYZICO_SANDBOX', true);   // test hesabı; canlıda false
   // PayTR (iFrame API)
   define('VERNUKO_PAYTR_MERCHANT_ID', '...');
   define('VERNUKO_PAYTR_MERCHANT_KEY', '...');
   define('VERNUKO_PAYTR_MERCHANT_SALT', '...');
   define('VERNUKO_PAYTR_TEST', true);       // test ödemesi; canlıda false
   ```
2. **PayTR:** Mağaza paneli › Destek & Kurulum › Ayarlar › Bildirim URL'ye eklentinin gösterdiği adresi yazın: `https://siteniz.com/wp-json/vernuko/v1/pay/paytr`. Siteniz SSL'liyse https seçin.
3. Vernuko Lisans › **Paketler ve satış**: paketleri girin. Her pakette kısa ad, fiyat (KDV dahil TL), tür (yeni ya da yenileme), bilgisayar sayısı ve gün olur. Aynı sayfadan şu sayfaların adreslerini de verin:
   - mesafeli satış sözleşmesi ve ön bilgilendirme formu,
   - KVKK aydınlatma metni,
   - Lisanslarım sayfası,
   - yenileme sayfası.
4. Sayfalar (temanızın içine istediğiniz yere):
   - Satın alma: `[vernuko_satin_al plan="yillik"]`
   - Yenileme: `[vernuko_satin_al plan="yenileme"]`. Lisanslarım'daki "Yenile" düğmesi bu sayfayı anahtarı doldurulmuş olarak açar.
   - Lisanslarım: `[vernuko_lisanslarim]`. Müşteri e-postasını yazar, 30 dakika geçerli bir giriş bağlantısı alır (parola yok). Bu sayfada anahtarını, bilgisayarlarını ve bitiş tarihini görür; bozulan bilgisayarı kaldırabilir.

**Akış:** Müşteri formu doldurur ve sözleşme ile KVKK kutularını işaretler. Ardından iyzico'nun ödeme sayfasına gider ya da PayTR ödeme çerçevesi sayfanızda açılır.
- **Lisansın oluşması:** yalnızca sağlayıcının **imzalı onayı** gelince ve ödenen tutar paketin fiyatına eşit ya da fazlaysa. Taksit farkı nedeniyle fazla olabilir; eksik ödeme kabul edilmez. iyzico'da sonuç iyzico'dan sorulup imzası doğrulanır; PayTR'de bildirimin imzası doğrulanır.
- Anahtar e-postayla gelir. Siparişi veren tarayıcıda ayrıca ekranda görünür.
- Aynı onay iki kez gelirse tek lisans oluşur ve tek e-posta gider.
- **iyzico'da müşteri siteye geri dönmezse** (sekmeyi kapattı, bağlantı koptu): sipariş durum sayfası ve Siparişler'deki **"iyzico'ya sor"** düğmesi sonucu iyzico'dan yeniden sorar. iyzico'nun dolandırıcılık incelemesindeki ödeme "bekliyor"da kalır, başarısız sayılmaz; inceleme bitince aynı yolla tamamlanır.
- Sipariş, verildiği andaki paketi (bilgisayar sayısı, gün, paket adı) kendisiyle saklar: ödeme sırasında paketi değiştirmek ya da silmek o siparişi etkilemez.
- Satın alma, Lisanslarım ve sipariş sonucu sayfaları önbelleğe alınmaz (`DONOTCACHEPAGE` + `no-store`): önbellek eklentisi bir müşterinin anahtarlarını başkasına göstermesin.
- Görünüm: formlar temanın yazı tipini ve düğme stilini (`wp-element-button`) kullanır; ince ayar için `.vernuko-checkout`, `.vernuko-licenses`, `.vernuko-card` sınıfları.

**Siparişler sayfası:**
- **İade:** parayı sağlayıcının panelinden iade ettikten sonra "İade edildi"ye basın. Lisans durur; müşterinin bilgisayarındaki veriler silinmez.
- **Muhasebe dosyası:** "Muhasebe için CSV indir" seçilen ayın satışlarını indirir: fatura bilgileri, tutar, ödeme numarası. Ay ve tarihler sitenin saat dilimindedir (Ayarlar › Genel). `=`, `+`, `-`, `@` ile başlayan müşteri metinleri Excel'de formül olarak çalışmasın diye başına `'` konur.
- **Fiyat:** `4.990,00`, `4990` veya `49,90` yazılabilir. Tek nokta ve 1–2 hane (`49.90`) ondalık sayılır. Okunamayan fiyat tahmin edilmez; eski fiyat korunur ve uyarı çıkar.
- **Fatura:** bu eklenti fatura kesmez. e-Arşiv için CSV'yi mali müşavirinize ya da fatura programınıza verin.

**Test ortamında doğrulanması gerekenler** (gerçek hesapla denenmedi):
- iyzico test sunucusunun adresi (`sandbox-api.iyzipay.com`; resmi örneklerde geçiyor).
- iyzico'nun başarılı ödeme durumu değeri (`SUCCESS`). Farklıysa lisans oluşmaz, sipariş "bekliyor"da kalır. Güvenli taraf bu.
- iyzico kimlik numarası: müşteri 11 haneli TCKN girmezse `11111111111` gönderilir. iyzico'ya bunun kabul edilip edilmediğini sorun.

## Dış satış sistemi
Satış WordPress dışında olduğunda o sistem lisans sunucusuna haber verir. `wp-config.php` içine en az 32 karakterlik rastgele bir sır yazın: `define('VERNUKO_LICENSE_SALES_SECRET', '...');`. Aynı sırrı dış sisteme de verin; sır iki sunucu dışında hiçbir yerde durmamalı.

```
POST https://siteniz.com/wp-json/vernuko/v1/sales
X-Vernuko-Signature: t=<unix saniye>,v1=<hex HMAC-SHA256(sır, t + "." + gövde)>
{"saleId":"SIPARIS-1001","kind":"new","customerName":"…","email":"…","edition":"Standart","devices":2,"days":365}
→ {"saleId":"SIPARIS-1001","licenseId":"lic_…","licenseKey":"VRNK-…","paidUntilUtc":"…","devices":2}

Yenileme:  {"saleId":"SIPARIS-1002","kind":"renewal","licenseKey":"VRNK-…","days":365}
İade:      POST …/v1/sales/revoke  {"saleId":"SIPARIS-1001"}
```
- **Kurallar:**
  - İmza 5 dakikadan eskiyse reddedilir.
  - Aynı `saleId` ikinci kez gelirse aynı lisans döner, yeni lisans açılmaz. Yeniden denemek güvenlidir.
  - İade edilmiş satış tekrar lisans açmaz.
- **Örnek imza (PHP):** `"t=$t,v1=" . hash_hmac('sha256', "$t.$body", $secret)`

## Yönetim
Vernuko Lisans sayfasından:
- elle lisans oluşturma (mağaza dışı satış),
- arama, anahtarı yeniden gösterme (denetim kaydına yazılır),
- +365 gün, iptal (ödenmiş dönem sonuna kadar çalışır),
- bilgisayar kaldırma.

## Güvenlik
- Özel imzalama anahtarı yalnızca `wp-config.php` dosyasındadır. "Anahtarları oluştur" onu yalnızca o yanıtta gösterir; veritabanına (geçici kayıt dahil) yazılmaz. Masaüstü uygulamada yalnızca açık anahtar bulunur; sahte lisans üretilemez.
- **Hesabım › Lisanslarım** yalnızca o hesapla alınmış lisansları gösterir. WooCommerce'te hesap e-postası doğrulama olmadan değiştirilebildiği için e-postayla eşleştirme yapılmaz; misafir olarak ya da kendi satış sayfasından alınan lisanslara Lisanslarım sayfasındaki e-posta bağlantısıyla erişilir.
- Aynı anda gelen iki etkinleştirme aynı boş yeri kapamaz: lisans satırı kilitlenerek sırayla işlenir.
- Güncelleme (zip yükleme, otomatik güncelleme) sonrası tablolar ilk istekte kendiliğinden güncellenir; eklentiyi kapatıp açmak gerekmez.
- Lisans anahtarı veritabanında saklanmaz, yalnızca SHA-256 özeti tutulur. Anahtar, lisans kimliği ve sırdan yeniden türetilir.
- Uç noktalar IP başına dakikada 30 istekle sınırlıdır. Yönetim işlemleri WordPress yetkisi (`manage_woocommerce`) ve nonce ister.
- Hazır "lisans yöneticisi" eklentilerinin aksine, mağazanızın API anahtarı programa konmaz.

## Neler denendi (kendi satış sayfası, 2026-11-13)
Gerçek WordPress'te, gerçek HTTP istekleriyle denendi. iyzico ve PayTR yerine, imzaları belgelerdeki formülle kontrol eden sahte sunucular kullanıldı.
- **Form:** onay kutuları olmadan ve hatalı bilgiyle ödeme başlamıyor.
- **iyzico:** yönlendirme ve dönüş çalışıyor. Lisans ve e-posta oluşuyor; dönüş tekrarlansa da tek lisans.
- **PayTR:** sahte bildirim reddediliyor; tekrar gelen bildirime "OK" dönüyor ama tek lisans oluşuyor. Onay gelene kadar sayfa bekliyor.
- **Lisanslarım:** kayıtsız adrese e-posta gitmiyor ve yanıt aynı. Oynanmış bağlantı girişi açmıyor. Bilgisayar kaldırma çalışıyor.
- **Yenileme:** süre uzuyor, e-posta gidiyor.
- **Dış satış bağlantısı:** imzasız ya da eski istek reddediliyor; tekrar ve iade doğru çalışıyor.
- PHP testleri: `tests/core-test.php` (33 kontrol) ve `tests/sales-test.php` (51 kontrol).
- **1.2.0 (2026-11-14):** güncel eklenti aynı sitede yeniden denendi: tablo yükseltmesi, müşteri dönmeden iyzico onayı, PayTR bildirimi, hesap e-postasını değiştirerek başkasının lisansını görme denemesi (görünmüyor), önbellek başlıkları, telefon ve masaüstü genişliğinde görünüm.

## Neler denendi (WooCommerce)
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
