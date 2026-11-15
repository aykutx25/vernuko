# Programlar: Vernuko Randevu, Vernuko Stok, Vernuko Teknik Servis

Üç program **tek kurulum dosyasıdır**. Hangisinin çalışacağını kurulum sihirbazında seçilen işletme türü belirler. Her program ayrı satılır ve kendi lisansıyla çalışır.

| Program | İşletme türleri (kurulumda) | Lisans ürünü | Açılış ekranı |
|---|---|---|---|
| Vernuko Randevu | güzellik, kuaför, klinik, danışmanlık… (önceki 14 profil) | `vernuko-desktop` | Randevular |
| Vernuko Stok | Hırdavat ve Yapı Malzemesi, Kırtasiye ve Kitapçı, Oto Yedek Parça, Toptan Satış, Mağaza (genel perakende) | `vernuko-stok` | Satış |
| Vernuko Teknik Servis | Telefon ve Tablet Servisi, Bilgisayar Servisi, Beyaz Eşya ve Klima Servisi, Elektronik Servis | `vernuko-teknik-servis` | Servis Fişleri |

- **Mod:** `BusinessProfileDefinition.ModeOf(profil)`.
- **Her programın kendi adı var.** Logonun yanında (üst çubuk, giriş ekranı, kurulum sihirbazı) ve pencere başlığında "Vernuko Randevu", "Vernuko Stok" ya da "Vernuko Teknik Servis" yazar.
- **Lisans:** `LicensingOptions.Product` boşsa lisansın ürünü moda göre belirlenir. Bir Vernuko Stok anahtarı Randevu kurulumunda kabul edilmez; tersi de geçerli.
- **Menü:** ana mod menüyü belirler. Teknik Servis, profilinde varsa Stok'un satış ve alış ekranlarını da gösterir (telefon servisinde aksesuar satışı gibi).
- **Randevuya özgü ekranlar yalnızca Vernuko Randevu'da var.** Stok ve Teknik Servis'te bunlar görünmez:
  - takvim, paketler, bekleme listesi ve randevu raporları;
  - müşteri kartında "Randevu ver", "Paket sat", randevu geçmişi ve paket sekmeleri;
  - ayarlarda randevu aralığı, otomatik onay ve randevu hatırlatma;
  - durum çubuğundaki hatırlatma bilgisi.
- **İşçilik:** Teknik Servis'te Hizmetler ekranının adı "İşçilik"tir; süre, oda ve cihaz alanları yoktur. Servis fişinde işçilik bu listeden seçilebilir.
- **Kurulum sihirbazı:** Stok modunda çalışma günü, çalışma saati, personel ve hizmet adımları atlanır. Teknik Servis'te "hizmetler" adımı "işçilik kalemleri" olarak görünür.
- **Ortak olanlar:** müşteriler ve cari hesap, kasa, kullanıcılar ve roller, denetim günlüğü, şifreli yedek, çoklu bilgisayar, mesajlar (SMS).

## Vernuko Stok

| Ekran | Ne yapar |
|---|---|
| **Satış** | Barkod okutma (aynı ürün tekrar okutulunca adet artar), kod veya adla arama, satır başına fiyat ve indirim, belge indirimi, nakit/kart/havale karışık ödeme, para üstü. Kayıtsız müşteri (perakende) tam ödeme yapar. Kayıtlı müşteride ödenmeyen kısım hesabına yazılır (veresiye). Raftakinden fazla satışta uyarı verir ama satışı durdurmaz. |
| **Bugün ve iade** | Gün sonu: net satış, nakit, kart, havale, veresiye, KDV oranına göre döküm, tahmini brüt kâr. Satış seçilip iade alınır: ürün stoğa döner. Kayıtsız satışta iade nakit verilir. Kayıtlı müşteride nakit verilmeyen kısım hesabına alacak yazılır. İadenin iadesi yapılamaz. |
| **Ürünler ve Stok** | Barkod (tekil), KDV oranı (%0/1/10/20), kategori, raf yeri, tedarikçi, kritik stok. |
| **Alış ve Tedarikçiler** | Tedarikçi kartı (VKN/TCKN ve IBAN doğrulamalı). Mal kabul: KDV hariç birim maliyet, satır indirimi, fatura no ve tarihi. İsteğe bağlı olarak ürün maliyetini günceller, şimdi ödenen tutarı düşer. Tedarikçi hesabı: hareketler ve bakiye, ödeme kaydı. |
| **Sayım ve Sipariş** | Sayım: saydıklarınızı yazarsınız, yalnızca farklar gerekçeli sayım düzeltmesi olarak kaydedilir (aynı sayım iki kez kaydedilmez). Sipariş önerisi tedarikçiye göre gruplanır; önerilen miktar stoğu kritik seviyenin iki katına tamamlar. Stok değeri maliyet ve satış fiyatıyla gösterilir. |

Belge numaraları: satış `S-YYYY-000001`, iade `SI-…`, alış `A-…`. Tüm hareketler eklenerek tutulur. Düzeltme ters kayıtla yapılır, kayıt silinmez.

### Yasal sınır: bu ekran mali fiş kesmez
- VUK 483 sayılı Genel Tebliğ m.8'e göre perakende satışta kullanılan yazılım, ÖKC'ye (yeni nesil yazar kasa) GMP-3 ile bağlanmalıdır. Bağlanmadan kullanılırsa özel usulsüzlük cezası söz konusudur. Ayrıntılar ve kaynaklar: [research/stock-mode-tr.md](research/stock-mode-tr.md).
- Vernuko'nun ÖKC bağlantısı **yok**. Satış ekranının üstünde bu yazılı: "Bu ekran yasal fiş kesmez…".
- Vernuko'yu perakende satışta nasıl kullanacağına (ör. fişi ÖKC'den kesip satışı Vernuko'ya da girmek) işletmenin mali müşaviri karar vermelidir. Bu konu doğrulanmadı.
- 1 Ocak 2026'dan itibaren e-Fatura kullanıcısı olmayanlar her faturayı e-Arşiv olarak düzenler (basit usul 2027'den). Vernuko e-Arşiv/e-Fatura **düzenlemez**; bunun için GİB portalı veya bir özel entegratör gerekir.

### Henüz yapılmadı (araştırmada MVP sayılanlar dahil)
- Ürün başına birden fazla barkod; koli/adet birim çarpanı.
- Toptan ve müşteriye özel fiyat listesi; toplu % fiyat güncelleme.
- Tedarikçiye iade (alış iadesi).
- Barkod ve raf etiketi yazdırma.
- Excel'den ürün ve açılış bakiyesi aktarımı.
- Satış ekstresini PDF olarak çıkarma.
- Terazi barkodu; çek/senet portföyü; döviz fiyat; seri/lot.
- e-Arşiv / e-Fatura (entegratörle); ÖKC (GMP-3) bağlantısı.

## Vernuko Teknik Servis

| Ekran | Ne yapar |
|---|---|
| **Servis Fişleri, liste** | Açık fişler (ya da hepsi). Fiş no, müşteri, telefon veya seri no ile arama. Durum, teslim sözü, "gecikti" ve "onay bekliyor" işaretleri. |
| **Cihaz kabul** | Müşteri, cihaz türü, marka, model, seri/IMEI, müşterinin anlattığı arıza, aksesuarlar, görünür durum, getiren, teknisyen, teslim sözü (boşsa 20 iş günü), yerinde servis ve adresi, isteğe bağlı cihaz şifresi. Seri numarası yazılınca cihazın önceki servis kayıtları gösterilir. |
| **İşlem ve onay** | Tespit ve yapılan işlem. Fiyat teklifi ve onaylanmazsa arıza tespit ücreti. Müşteri onayı kaydedilir: nasıl alındığı (yüz yüze, telefon, SMS, WhatsApp, imzalı form), kimin verdiği ve ne zaman. Onay beklenirken ya da reddedilmişken cihaz "onarımda" veya "hazır" yapılamaz. "Onarılamadı" için neden zorunludur. |
| **Parça ve işçilik** | Parça stoktan düşer; satır kaldırılırsa stoğa geri döner. İşçilik teknisyenle birlikte kaydedilir. |
| **Garanti** | Aynı seri numarası son 12 ay içinde teslim edildiyse fişte uyarı çıkar. Fiş garanti kapsamına alınınca işçilik ücretlendirilmez. Değişen parçalar teslimden itibaren 6 ay garantilidir. |
| **Kapora ve teslim** | Kapora kasaya girer. Teslimde nakit ve kart alınır; kalan tutar müşterinin hesabına yazılır. Teslimde cihaz şifresi silinir. |
| **Yazdırma** | Kabul belgesi ve servis fişi, Windows yazdırma penceresiyle (PDF'e yazdırma dahil) basılır. "Özel servis" ibaresi, garanti tarihleri ve imza alanları belgede yer alır. Cihaz şifresi hiçbir belgeye yazılmaz. |
| **SMS** | Mesajlar ayarlıysa, cihaz "teslime hazır" yapılınca müşteriye işletmenin kendi SMS hesabından mesaj gider. |

Fiş numarası: `SF-YYYY-000001`. Fişin her değişikliği geçmişe yazılır.

### Neden böyle (mevzuat özeti)
Kaynaklar: [research/service-mode-tr.md](research/service-mode-tr.md).

- **Kabul belgesi (SSHY m.11/1):** teslim eden ve alan, şikâyet, cihaz türü/marka/model, tarih.
- **Servis fişi (m.11/2):** servis bilgisi, teslim tarihi, arıza ve yapılan işlem, ücret, imzalar.
- **Garanti (m.10/6, m.10/7):** aynı arıza 1 yıl içinde ücretsiz onarılır; değişen parçalar en az 6 ay garantilidir.
- **Saklama (m.16):** 3 yıl. Vernuko kayıt silmez.
- **"Özel servis" ibaresi:** 6502 m.58/5 gereği bağımsız servisler bunu kullanmalı.
- **KVKK:** 2019'da bir teknik servis, takip numarasının son hanesi değiştirilerek başka müşterilerin bilgisine ulaşılabildiği için 150.000 TL ceza aldı. Vernuko'da dışarıdan sorgulanan bir durum sayfası bu yüzden yok. Durum bilgisi işletmenin kendi mesajıyla gider.
- **Cihaz şifresi:** yalnızca gerekiyorsa alınır. Şifreli saklanır (Windows'ta kullanıcının DPAPI anahtarıyla). Ekranda yalnızca "Göster" ile açılır; her görüntüleme denetim günlüğüne yazılır. Teslimde silinir.

### Henüz yapılmadı
- Kabulde ve teslimde cihaz fotoğrafı. Şifreli dosya altyapısı var; fişe bağlanmadı.
- Barkodlu/QR etiket ve termal yazıcı biçimi.
- Kabul belgesinin müşteriye SMS/WhatsApp ile gönderilmesi ve teslim onayının kaydı (m.11/6).
- Bakım sözleşmesi ve periyodik klima bakımı.
- Teknisyen raporları (iş süresi, ciro).
- Teknisyen primi.
- e-Arşiv; çevrim içi durum sorgulama (yukarıdaki KVKK nedeniyle tasarım gerektirir).

## Demolar
Her programın kendi demosu ve kendi demo veritabanı vardır. Gerçek kayıtlara dokunmazlar:
- Vernuko Randevu: `demo.cmd` ya da `--demo`.
- Vernuko Stok: `demo-stok.cmd` ya da `--demo-stok`. Örnek veri: hırdavat dükkânı; ürünler, iki tedarikçi, mal kabul, bugünkü satışlar, veresiye, iade.
- Vernuko Teknik Servis: `demo-servis.cmd` ya da `--demo-servis`. Örnek veri: telefon servisi; her durumda birer servis fişi (teslim alındı, onay bekliyor, onarımda, parça bekliyor, hazır, teslim edildi), parçalar, kapora, aksesuar satışı.

## Doğrulama durumu
- İş kuralları ve ekranların view model'leri otomatik testlerle doğrulandı:
  - `StockModeTests` (6) ve `TechnicalServiceModeTests` (5) servis katmanını sınar.
  - `ModeViewModelTests` (3) ekranları uçtan uca sürer: sihirbazdan girişe ve menüye, oradan mal kabul, satış, iade ve sayıma; servis fişinde kabulden teslime ve garanti bağlantısına.
- Windows smoke testi Stok ve Teknik Servis'i kendi demolarıyla açar ve şunları kontrol eder:
  - giriş ekranında, üst çubukta ve pencere başlığında programın kendi adı yazıyor mu;
  - menüde randevu ekranı var mı (olmamalı).
- Ardından her menü sayfasının her sekmesini üç boyutta, servis belgelerini de çizer ve bağlama hatalarını denetler (CI).
- **Gerçek bir dükkânda, barkod okuyucu ve yazıcıyla denenmedi.**
