# Değişiklik Günlüğü

Biçim: [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/). Sürümleme: [SemVer](https://semver.org/lang/tr/).

## [0.11.0] — 2026-11-13 — Kendi satış sayfası (iyzico + PayTR) ve dış satış bağlantısı

### Eklendi
- **WooCommerce'siz satış:** satın alma sayfası (`[vernuko_satin_al plan="…"]`). Ödeme **iyzico** (Ortak Ödeme Sayfası) veya **PayTR** (iFrame) ile yapılır; ikisi aynı anda açık olabilir, müşteri seçer.
  - Lisans yalnızca sağlayıcının imzalı onayı gelince ve tutar tutunca oluşur. Aynı onay iki kez gelirse tek lisans ve tek e-posta.
  - Formda mesafeli satış sözleşmesi ve KVKK onayı var.
- **Lisanslarım** (`[vernuko_lisanslarim]`): parola yok, e-postaya gelen 30 dakikalık bağlantıyla giriş. Anahtar, bilgisayarlar ve bitiş tarihi görünür; bilgisayar kaldırılabilir, lisans yenilenebilir.
- **Yönetici ekranı:** Siparişler (iade işaretleme, muhasebe için aylık CSV) ve Paketler (fiyat, süre, bilgisayar sayısı, sayfa bağlantıları).
- **Dış satış sistemi bağlantısı:** WordPress dışındaki bir satış sistemi, imzalı istekle lisans açar, yeniler ve iade eder. Aynı satış iki kez işlenmez.
- WooCommerce desteği aynen sürüyor. Üç yol aynı lisans tablolarını ve aynı "satış → tek lisans" kuralını kullanır.

### Düzeltildi (denemede bulundu)
- Form alanı adları WordPress'in ayrılmış parametreleriyle çakışıyordu ("name" alanı "sayfa bulunamadı" açıyordu). Alanlar `vk_` önekiyle değiştirildi.

### Denenmedi
- Gerçek iyzico/PayTR hesabıyla ödeme. İstek ve imza biçimleri resmi belgelerden alındı ve testlerle doğrulandı; iyzico'nun test sunucusu adresi ve "başarılı" durum değeri ilk test ödemesinde kontrol edilmeli.

## [0.10.0] — 2026-11-13 — Lisans sunucusu WordPress'te

### Eklendi
- **WordPress eklentisi "Vernuko Lisans"** (`wordpress/vernuko-license`, kurulum `README.md`). Satış sitesinin içinde çalışır, ayrı sunucu gerekmez.
  - WooCommerce'te "Vernuko lisansı" işaretli ürün satılınca, ödeme tamamlandığında lisans anahtarı oluşur. Anahtar müşterinin sipariş e-postasında, sipariş sayfasında ve Hesabım › Lisanslarım'da görünür.
  - Yenileme süreyi dönem sonuna ekler. İade ve iptal lisansı durdurur.
  - Müşteri bozulan bilgisayarını kendisi kaldırabilir.
  - Yönetici ekranı: elle lisans, uzatma, iptal, bilgisayar kaldırma, anahtarı yeniden gösterme.
  - İmzalama anahtarları `wp-config.php` içindedir; veritabanına yazılmaz.
- Masaüstü uygulama değişmedi. Eklenti .NET lisans sunucusuyla aynı protokolü ve aynı imzalı belgeyi kullanır.
- CI: PHP çekirdek testleri ve kurulabilir `vernuko-license.zip` paketi.

### Doğrulandı
- Gerçek WordPress + WooCommerce 11.3 kurulumunda satış, e-posta içeriği, çift işlem koruması, yenileme, iade, Hesabım ve yönetici ekranı.
- Masaüstü istemcisi gerçek siteden etkinleştirme, yenileme ve kaldırma yaptı.

### Denenmedi
- MySQL'li gerçek barındırma, gerçek iyzico/PayTR ödemesi, gerçek e-posta gönderimi.

## [0.9.0] — 2026-11-12 — Faz 7: çoklu bilgisayar

### Eklendi
- **Ana bilgisayar + bağlanan bilgisayarlar** (ayrıntı `docs/MULTI_PC.md`): işletmedeki bir bilgisayar Ayarlar › Çoklu bilgisayar'dan "ana bilgisayar" yapılır. Diğer bilgisayarlar kurulum sihirbazında "Ana bilgisayara bağlan" ile, ana bilgisayarın ekranındaki adres ve tek kullanımlık bağlantı koduyla eşleşir. Aynı müşteriler, randevular, paketler ve kullanıcılar her bilgisayarda görünür. Veritabanı, yedekleme, otomatik mesajlar ve lisans yalnızca ana bilgisayardadır. Müşteriye ek program kurulmaz.
- Aynı saate iki bilgisayardan randevu verilmek istenirse yalnızca biri kaydedilir (çakışma kuralı ana bilgisayarda).
- Her kişinin yetkisi ana bilgisayarda kendi oturumuyla denetlenir. Bilgisayarı kilitlemek yalnızca o oturumu kilitler.
- Güvenlik: bağlantı şifreli. Ana bilgisayarın sertifikası eşleştirmede sabitlenir; araya giren bir cihaz fark edilir. Her bilgisayarın kendi anahtarı var ve ana bilgisayardaki listeden tek tıkla kaldırılabilir; kaldırılınca açık oturumu hemen kapanır. Kaç bilgisayar bağlanabileceği lisansa göre belirlenir (deneme sürümünde 2).
- İç yapı: ekranlar artık servis arayüzlerine bağlı (`ServiceContracts.cs`, derlenmiş servislerden üretilir).

### Bilinen sınırlar
- İki gerçek bilgisayarda yerel ağda **henüz denenmedi**; testler aynı makinede gerçek TLS bağlantısıyla yapıldı.
- Ana bilgisayar kapalıyken bağlı bilgisayarlar çalışmaz. Başka bilgisayarda verilen randevu, takvimde en geç bir dakika içinde görünür (anlık bildirim yok).

## [0.8.0] — 2026-11-12 — Faz 3: firma müşterisi

### Eklendi
- **Firma bilgileri** (Danışmanlık ve Muhasebe profili; bilgisi girilmiş her müşteride de görünür): firma unvanı, vergi dairesi, vergi numarası. 10 haneli VKN kontrol hanesiyle doğrulanır; şahıs şirketleri için 11 haneli T.C. kimlik numarası kabul edilir. Unvan ve vergi numarasıyla arama yapılabilir. Bilgiler JSON/CSV dışa aktarımına girer, anonimleştirmede silinir.

## [0.7.0] — 2026-11-12 — Faz 6: lisans sunucusu

### Eklendi
- **Lisans sunucusu** (`src/Vernuko.LicenseServer`, ayrıntı `docs/LICENSE_SERVER.md`): lisans anahtarıyla etkinleştirme, haftalık yenileme, cihaz sınırı, yeni bilgisayara taşıma, sunucu tarafı deneme kaydı, güncelleme izni, elle lisans oluşturma/uzatma/iptal (yönetici anahtarıyla), denetim kaydı, IP başına istek sınırı. Sunucuya müşteri ve randevu verisi gitmez.
- **Paddle Billing webhook'u:** imza doğrulama, aynı olay iki kez gelirse tek işlem, geç gelen eski olayların yok sayılması, 7 günlük ödeme toleransı. Gerçek bir Paddle hesabıyla **henüz denenmedi**.
- **Ayarlar › Lisans:** anahtarla etkinleştirme, "Şimdi doğrula", "Bu bilgisayardan kaldır (taşıma)". Program açıkken lisans kendiliğinden yenilenir. İnternet yoksa çevrimdışı süre boyunca çalışmaya devam eder.
- Sunucudan verilen güncellemeler yalnızca etkinleştirilmiş kurulumlara iner.
- CI'da Linux için lisans sunucusu paketi üretiliyor.

### Bilinen sınırlar
- iyzico webhook'u yok; TL satışı elle lisans oluşturmayla yapılır. Anahtarın e-postayla gönderimi ve yönetici ekranı yok.
- Sunucu henüz bir VPS'e kurulmadı. Sunucu adresi ve açık anahtar tanımlanana kadar masaüstü "lisans sunucusu tanımlı değil" der; lisans dosyası yükleme çalışmaya devam eder.

## [0.6.0] — 2026-11-12 — Faz 3: hizmete özel oda ve cihaz kuralı

### Eklendi
- **Hizmet yalnızca belirli oda/cihazlarda:** Hizmetler ekranında "Yalnızca şu oda / cihazlarda" listesi. Örnek: Lazer Epilasyon → Lazer Odası 1, Lazer Odası 2 ve Lazer Cihazı. Lazer cilt bakım odasına ya da cihazsız verilmek istenirse randevu **kaydedilmez** (onayla da geçilemez) ve uygun oda/cihazlar yazılır. Hiçbiri işaretli değilse eski davranış sürer.
- Kurulum sihirbazı, Güzellik ve Lazer profilinde bu eşleşmeleri oda/cihaz adlarından kendisi kurar (lazer → "Lazer" geçen oda ve cihazlar, cilt bakımı → "Cilt Bakım" odası). Sonradan Hizmetler ekranından değiştirilebilir.
- **Randevu paneli:** hizmet seçilince uygun oda ve cihaz kendiliğinden işaretlenir, uygun olmayan oda kaldırılır, kural panelde yazılır. Önerilen saatler diğer uygun odaları da tarar: Lazer Odası 1 doluysa "10:00 · Lazer Odası 2" gibi gösterilir; tıklanınca oda değişir.
- Demo verisi bu kurallara uyar; kurallara uymayan demo randevusu sessizce atlanmaz, hata verir.

## [0.5.0] — 2026-11-12 — Faz 5: otomatik randevu hatırlatma

### Eklendi
- **Otomatik hatırlatma:** randevudan belirli saat önce (varsayılan 24) SMS veya WhatsApp. Giden kutusu sayesinde aynı randevu için ikinci mesaj oluşmaz; iptal edilen veya taşınan randevunun bekleyen hatırlatması iptal edilir, yeni saate yeni hatırlatma planlanır.
- **İleti Merkezi SMS** ve **WhatsApp Cloud API** bağlantıları: işletmenin kendi hesabı; istek ve yanıt biçimleri resmi belgelerden alındı ve testlerle doğrulandı. **Gerçek bir hesapla henüz denenmedi** — ilk kullanımda "Deneme mesajı gönder" ile kontrol edin.
- **Maliyet kontrolü:** varsayılan kapalı ve aylık limit 0 (hiç mesaj gitmez); limit dolunca gönderim durur; sessiz saatler (21:00–09:00); müşteri izni gönderim anında yeniden kontrol edilir; lisans salt-okunurken gönderim yapılmaz.
- Ayarlar › Mesajlaşma: kanal, zamanlama, limit, SMS birim fiyatı (tahmin için), şifreli saklanan hesap bilgileri, deneme mesajı, giden mesajlar ve aylık özet. "Entegrasyonlar ve Maliyetler" gerçek durumu gösterir.

### Bilinen sınırlar
- Netgsm desteği yok (API belgesine erişilemedi); Verimor yok (gönderen IP beyaz listesi zorunlu).
- Teslim raporu alınmıyor: durum "sağlayıcıya iletildi" olarak tutulur.

## [0.4.0] — 2026-11-12 — Faz 4: bulut kopyası

### Eklendi
- **Bulut kopyası (OneDrive / Google Drive / Dropbox):** her düzenli yedeğin şifreli kopyası bilgisayardaki bulut eşitleme klasörüne (`Vernuko Yedekler`) konur; yüklemeyi bulut uygulaması müşterinin kendi hesabına yapar. Klasörler otomatik bulunur (Türkçe Windows'taki "Drive'ım" dahil) ya da elle seçilir. Varsayılan kapalı; açmak için KVKK yurt dışı aktarım bilgilendirmesinin onaylanması gerekir. Bulut klasörüne ulaşılamazsa yerel yedek yine alınır, sorun ekranda gösterilir. Aynı saklama politikası bulutta da uygulanır. Durum çubuğu ve "Entegrasyonlar ve Maliyetler" gerçek durumu gösterir.
- GitHub Actions yeniden her değişiklikte çalışıyor (depo açık): Windows'ta derleme, testler, tüm ekranlar, başarı senaryosu ve kurulum paketi otomatik denetleniyor.

### Düzeltildi
- Ayarlar › Yedekleme'deki "fotoğraf/ek dosya desteği henüz yoktur" yazısı güncel değildi (fotoğraflar Faz 2'den beri yedekte).

## [0.3.0] — 2026-11-12 — Açık kaynak (AGPL-3.0)

### Değişti
- **Lisans: AGPL-3.0-only** (`LICENSE`). Kaynak kod açık; değiştirip dağıtan kendi kodunu da açmak zorunda. "Vernuko" adı ve logosu lisans kapsamında değil.
- Program, lisans metnini (`LICENSE.txt`) ve açık kaynak bileşen bildirimlerini (`THIRD-PARTY-NOTICES.md`) birlikte dağıtır; Ayarlar › Lisans'ta açık kaynak bilgisi ve kaynak kod bağlantısı var.
- Belgeler: lisans kararı ve sonuçları (`docs/LICENSING.md`), açık kaynak imza seçenekleri (`docs/RELEASE.md`).

## [0.2.10] — 2026-11-12

### Eklendi
- **Yeni bilgisayara taşıma:** kurulum sihirbazının ilk ekranında "Yedekten geri yükle…". Eski bilgisayardan alınan şifreli yedek (.vbk) ve kurtarma anahtarıyla tüm kayıtlar (kullanıcılar, müşteriler, paketler, ödemeler, fotoğraflar) yeni bilgisayara gelir; kurulum adımları yeniden yapılmaz, eski kullanıcı adı ve parolayla giriş yapılır. Güvenlik: bu yol yalnızca henüz kurulmamış bir bilgisayarda açık; yanlış anahtar reddedilir.
- Giriş ekranı bilgi mesajı gösterebiliyor (ör. "Yedek bu bilgisayara yüklendi").

## [0.2.9] — 2026-11-12

### Doğrulandı
- Temiz bir Windows 11 bilgisayarda: 143/143 test ve **başarı senaryosu gerçek ekranlarda baştan sona** (paket 8 → 7, şifreli yedek, geri yükleme) geçti.

### Düzeltildi
- Kontrol raporu Windows PowerShell'de Türkçe harfleri bozuk gösteriyordu ("MÃ¼ÅŸteri"); rapor UTF-8 BOM ile yazılıyor ve `kontrol.cmd` doğru kodlamayla gösteriyor.
- Yeni Windows 11 bilgisayarda imzasız `Vernuko.exe` Akıllı Uygulama Denetimi tarafından engellendi; `kontrol.cmd`, `baslat.cmd`, `demo.cmd` programı Microsoft imzalı `dotnet` üzerinden başlatıyor. Kod imzalama satış için engelleyici iş olarak işaretlendi.

## [0.2.8] — 2026-11-12

### Eklendi
- **Başarı senaryosu otomatik:** `kontrol.cmd` artık gerçek Windows ekranlarında müşteri ekler, 8 seanslık paket satar, paketten randevu verir, "geldi" ve "tamamla" yapar, kalan seansın 7 olduğunu doğrular, şifreli yedek alır, sonradan bir kayıt ekler, yedekten geri yükler ve kaydın geri alındığını, 7 seansın korunduğunu doğrular. Her adımın ekran görüntüsü `vernuko-kontrol\senaryo-*.png` olarak kaydedilir. Aynı senaryo Linux testlerinde de çalışır.

## [0.2.7] — 2026-11-12

### Değişti
- **İşleme başlama ve tamamlama en erken randevu saatinden 1 saat önce yapılabilir** (önceden tam saatinde). Erken gelen müşteri boş odada erken alınabilir; yarınki bir randevu yine yanlışlıkla tamamlanamaz (paketten seans düşürür). "Gelmedi" yalnızca randevu saati geçince işaretlenebilir.
- Randevu satırındaki işlemler menüsü artık o an seçilemeyecek durumları göstermiyor (ör. yarınki randevuda "Tamamla" yok). Windows'taki denemede, menüde görünen "Tamamla" seçilince "Randevu saati gelmeden bu durum seçilemez" uyarısı çıkıyordu.

## [0.2.6] — 2026-11-12

### Düzeltildi
- Arka planda çalışan zamanlanmış yedekleme veya ekran geçişi (giriş, çıkış, kurulum sonrası) beklenmeyen bir hata aldığında süreci düşürebiliyordu. Artık hata günlüğe yazılır ve ekranda bildirilir, program çalışmaya devam eder. Windows'ta yerel kontrolde (`kontrol.cmd`) bulundu: 141 testin hepsi geçti ama bittikten sonra arka planda süren bir yedek işi test sürecini çökertti.
- Kurulum sihirbazı testi artık arka plandaki ilk yedeğin bitmesini bekliyor (zamanlamaya bağlı kararsızlık giderildi).

## [0.2.5] — 2026-11-12

### Düzeltildi
- **Programı kapatırken hata çıkıyordu.** Yedekleme kapalıyken (ör. demo modu) kapanış kontrolü anında bittiği için pencere, Windows kapanma işlemi sürerken ikinci kez kapatılmaya çalışılıyor ve "Beklenmeyen bir hata oluştu" uyarısı çıkıyordu; "yedek alınıyor" yazısı ekranda kalabiliyordu. Kapanış artık sırayla yapılıyor; kapanırken ikinci kez X'e basılması ve yedekleme hatası kapanmayı engellemiyor.
- Smoke test ana pencereyi açıp kapatarak bu durumu denetler; kontrol modunda beklenmeyen hatalar uyarı kutusu açmak yerine rapora yazılır.

## [0.2.4] — 2026-11-12

### Eklendi
- Çalışan sürüm numarası giriş ekranında ve alt durum çubuğunda görünür (güncellemenin gelip gelmediği anlaşılsın diye).

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
