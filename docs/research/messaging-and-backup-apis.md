> **Araştırma notu.** Bu rapor 11–12 Kasım 2026'da resmi web siteleri, yardım merkezleri, fiyat sayfaları ve herkese açık kullanıcı yorumları taranarak hazırlanmıştır. Her iddia kaynağıyla birlikte "doğrulandı" / "doğrulanamadı" olarak işaretlenmiştir. Fiyatlar ve politikalar sık değişir; satın alma veya hukuki karar öncesinde ilgili kaynak yeniden kontrol edilmelidir. Hukuki konular avukat görüşü yerine geçmez.

# Vernuko: Mesajlaşma ve Yedekleme API'leri, Teknik Entegrasyon Araştırması

**Kapsam:** Windows masaüstü (C#/.NET 10, WPF), çevrimdışı öncelikli randevu uygulaması. İşletme kendi hesaplarını bağlar (bulut depolama, WhatsApp, SMS), satıcı müşteri başına işletme maliyeti üstlenmez.
**Erişim tarihi:** 11-12 Kasım 2026. Kaynak olarak yalnızca resmi dokümantasyon, standart ve RFC'ler kullanıldı.
**İşaretleme:** (doğrulandı: URL) ilgili ifadenin o sayfada okunduğunu gösterir. (doğrulanamadı) birincil kaynakta teyit edilemeyen, çıkarım ya da genel bilgi niteliğindeki ifadeleri gösterir. "Hesaplandı" ile işaretli sayılar, doğrulanmış kurallardan türetilmiştir.

---

## 0. Karar açısından kritik bulgular (özet)

1. **WhatsApp: 1 Ekim 2026'dan beri pencere içi utility mesajları da ücretli.** Servis mesajları da yeniden ücretli, ama her numaraya ayda 1.000 ücretsiz servis mesajı veriliyor. Türkiye ayrı bir pazar. USD fiyatları: utility, authentication ve service 0,0009 USD, marketing 0,0109 USD.
2. **Teslim ve okundu bilgisi yalnızca webhook ile geliyor.** Geçmiş webhook verisini getiren bir API yok ve uç nokta herkese açık, geçerli TLS sertifikalı olmalı. Seçenekler: yalnızca gönderim, işletmenin kendi aktarıcısı, satıcının aktarıcısı (satıcıya maliyet çıkarır) ya da BSP.
3. **Google Drive: `drive.file` ve `drive.appdata` non-sensitive kapsamlar, CASA gerekmiyor.** Restricted `drive` kapsamı yıllık CASA ister. Kotalar satıcının Cloud projesine yazılıyor ve 2026'dan itibaren günlük bir faturalama eşiği var.
4. **OneDrive: `Files.ReadWrite.AppFolder` kişisel hesaplarda kesin çalışıyor.** İş ve okul hesaplarında dokümanlar çelişiyor, test edilmeli. Parçalar 320 KiB'nin katı olmalı.
5. **Dropbox: App Folder, PKCE ve `token_access_type=offline` ile kalıcı refresh token alınıyor.** 50 bağlı kullanıcıdan sonra iki hafta içinde production onayı şart. `Dropbox.Api` 7.4.0 Eylül 2026'da çıktı, lisansı MIT.
6. **Yedekleme:** `VACUUM INTO`, ardından STREAM tarzında parçalı AES-256-GCM (indeks ve son parça bayrağı), ardından yükleme. Anahtar Argon2id ya da PBKDF2 ile kurtarma parolasından türetilir. DPAPI yalnızca yerel önbellek için.
7. **Türkçe SMS:** ğ, ı, İ, ş, Ş ya da ç içeren ileti UCS-2'ye düşer (70/67). Türkçe single shift ile kapasite 155, birleştirilmiş iletide parça başına 149 septet olur (hesaplandı).

---

## 1. WhatsApp Business Platform / Cloud API

### 1.1 Fiyatlandırma modeli (Kasım 2026 durumu)

- **Mesaj başına ücretlendirme 1 Temmuz 2025'te başladı.** Yalnızca işletmeden kullanıcıya giden ve teslim edilen mesajlar ücretlendirilir. Fiyat, kategori ile alıcının ülke kodunun (market) birleşimine göre belirlenir. Kullanıcıdan işletmeye gelen mesajlar ücretsizdir (doğrulandı: [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).
- **Kategoriler:** marketing, utility ve authentication şablon mesajlarıdır ve her zaman gönderilebilir. Service, açık bir 24 saatlik pencere içinde gönderilen şablonsuz mesajdır. 1 Temmuz 2026'da beşinci kategori olarak **Meta Business Agent** eklendi. Bu kategori şablonsuz ve yapay zekâ destekli yanıtları kapsar, 1 milyon token başına 2,00 USD ücretlendirilir (doğrulandı: [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).
- **1 Ekim 2026 değişikliği:** Meta, 1 Kasım 2024'ten beri ücretsiz olan servis mesajlarını ve 1 Temmuz 2025'ten beri ücretsiz olan pencere içi utility mesajlarını yeniden ücretlendirmeye başladı. Status webhook'larındaki `pricing.type` alanı `free_customer_service` yerine `regular` oldu. Her işletme telefon numarasına ayda **1.000 ücretsiz servis mesajı** tanınıyor ve kullanılmayan hak devretmiyor. 30 Eylül 2026'ya kadar ödeme yöntemi eklemeyen hesapların servis mesajları teslim edilmiyor (doğrulandı: [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).
- **FEP:** Click-to-WhatsApp reklamından gelen kullanıcıya verilen yanıt 7 güne kadar ücretsiz bir pencere açar (doğrulandı: [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)). Hatırlatma senaryosu için önemsiz.
- **Fiyat kartları ve Türkiye:** Meta 47 market için fiyat yayımlıyor: 39 ülke ve 8 bölge. Türkiye (+90) ayrı bir market olarak listelenmiş. Fiyatlar 16 para birimi için CSV ve PDF olarak yayımlanıyor ve **listede TRY yok**. Para birimi hesap açılırken seçiliyor ve sonradan değiştirilemiyor, yalnızca Currency Migration API ile taşınabiliyor (doğrulandı: [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).
- **Türkiye USD fiyatları (1 Ekim 2026'dan itibaren):** Marketing 0,0109; Utility 0,0009; Authentication 0,0009; Service 0,0009 USD. Authentication-International kategorisi Türkiye için tanımlı değil. 1 Nisan 2026'da Türkiye'nin utility ve authentication fiyatları düşürülmüştü (doğrulandı: pricing sayfasındaki "USD list rates" dosyası ve güncellemeler tablosu, [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).
- **Hacim kademeleri** yalnızca utility ve authentication mesajlarına uygulanır, her ay sıfırlanır ve işletme portföyü düzeyinde toplanır (doğrulandı: [WA Pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing)).

*Sonuç:* Ayda 1.000 utility hatırlatma yaklaşık 0,90 USD tutar. Bu bedel işletmenin kendi WhatsApp hesabına (WABA) bağlı ödeme yönteminden tahsil edilir, satıcıya yansımaz.

### 1.2 Şablonlar, onay ve kategoriler

- **Utility şablonları:** Promosyon içermemeli ve kullanıcının siparişi, hesabı ya da hizmetiyle doğrudan ilgili olmalı veya kullanıcı için kritik olmalı. Bu iki ölçütün ikisi de aranır (doğrulandı: [Template categorization](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/template-categorization)). Randevu hatırlatması bu tanıma uyuyor. Ancak Meta'nın örnek listesinde açıkça bir "appointment reminder" örneği bulunamadı (doğrulanamadı, kategori ölçütlerinden çıkarım).
- **İnceleme süresi** en fazla 24 saat (doğrulandı: [Templates overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/overview)).
- **Otomatik yeniden kategorilendirme:** 1 Temmuz 2024'ten beri onaylı şablonlar periyodik olarak denetleniyor. Pazarlama niteliği taşıyan bir utility şablonu bir gün önceden bildirimle marketing'e çevriliyor. 16 Nisan 2025'ten beri kötüye kullanım uyarısı almış işletmelere bu bildirim de yapılmıyor (doğrulandı: [Template categorization](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/template-categorization)). Bu yüzden hatırlatma şablonlarına kampanya ya da indirim metni eklenmemeli.
- **Direct Send (2026):** Şablonsuz utility gönderimi. Yalnızca mesaj limiti 100.000 ya da sınırsız olan portföylere açık, KOBİ'ler için geçerli değil (doğrulandı: [Direct Send](https://developers.facebook.com/documentation/business-messaging/whatsapp/direct-send/)).

### 1.3 24 saatlik müşteri hizmetleri penceresi

Kullanıcının her mesajı ya da araması 24 saatlik pencereyi açar veya sıfırlar. Pencere açıkken şablonsuz mesaj, kapalıyken yalnızca onaylı şablon gönderilebilir. Her durumda opt-in gerekir (doğrulandı: [Send messages](https://developers.facebook.com/documentation/business-messaging/whatsapp/messages/send-messages)).

### 1.4 Webhook'lar ve sunucusuz masaüstü uygulaması sorunu

- Status webhook'ları şu durumları bildirir: sent, delivered, read, gönderilemedi ve teslim edilemedi. Okunma, teslimatı da kapsar (doğrulandı: [Status webhook](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/reference/messages/status/)).
- Gönderim API'sinin döndürdüğü yanıt yalnızca isteğin kabul edildiğini gösterir, **teslimatı göstermez**. Teslim durumu yalnızca webhook ile gelir (doğrulandı: [Send messages](https://developers.facebook.com/documentation/business-messaging/whatsapp/messages/send-messages)).
- Üretimde uç nokta herkese açık bir sunucuda, geçerli TLS sertifikasıyla çalışmalı. **Self-signed desteklenmez.** POST'lar app secret ile hesaplanmış HMAC-SHA256 içeren `X-Hub-Signature-256` başlığıyla gelir. **Geçmiş webhook verisini getiren API yoktur.** 200 dışı yanıtlarda Meta 7 güne kadar yeniden dener ve bu mükerrer bildirime yol açabilir. Bir POST en fazla 1.000 güncelleme taşır (doğrulandı: [Create webhook endpoint](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/create-webhook-endpoint), [Webhooks overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview)).
- Callback URL'si WABA ya da telefon numarası düzeyinde ayrıca tanımlanabilir (override) (doğrulandı: [Webhooks overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview)).
- **Veri modeli etkisi:** Kullanıcı bir kullanıcı adı edinmişse `wa_id` ve `from` alanları boş gelebilir. Bu durumda kullanıcıyı tanımlamak için BSUID (business-scoped user ID) kullanılmalı (doğrulandı: [Webhooks overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview)). Bu yüzden müşteri tablosunda telefon numarasının yanında `wa_bsuid` alanı da tutulmalı.

**Masaüstü için seçenekler** (mimari değerlendirme, fiyatlar doğrulanmadı):

| Seçenek | Satıcıya maliyet | Not |
|---|---|---|
| A. Yalnızca gönderim | Yok | Ne teslim/okundu bilgisi ne de gelen yanıt alınır. Hatırlatmalar için asgari çözüm. |
| B. İşletmenin kendi hesabında çalışan aktarıcı (serverless fonksiyon ve kuyruk) | Yok | HMAC doğrulaması aktarıcıda yapılır. Masaüstü uygulaması kuyruğu sorgular. App secret aktarıcıda saklanır. Kurulumu teknik bilgi ister. Ücretsiz katman koşulları doğrulanamadı. |
| C. Satıcının işlettiği çok kiracılı aktarıcı | Düşük ama sıfır değil | Override URL'leri ile tek uç nokta kullanılabilir. Satıcı, sağlık verisi açısından KVKK kapsamında veri işleyen konumuna girebilir (doğrulanamadı, hukuki görüş gerekir). |
| D. BSP | İşletme öder | BSP'lerin kendi gelen kutusu ve API'si olur. Ücretleri doğrulanamadı. |

Her seçenekte kuyruk en az 7 günlük tekrar süresini kaldırmalı ve mükerrer kayıtlar `wamid` ve durum alanlarıyla ayıklanmalı.

### 1.5 Hesap modeli ve belirteçler

- **Doğrudan geliştirici** yalnızca kendi işletmesinin verisine erişir ve System User access token kullanır. **Tech Provider** ise Business Integration System User token kullanır (doğrulandı: [Access tokens](https://developers.facebook.com/documentation/business-messaging/whatsapp/access-tokens/)).
- Gelişmiş izin isteyen ya da başka işletmelerin kendi verisine erişmesini sağlayan uygulamalar doğrulanmış bir işletmeye bağlı olmalı. Yalnızca uygulamada rolü olan kullanıcılar kullanıyorsa doğrulama gerekmez (doğrulandı: [Business Verification](https://developers.facebook.com/documentation/development/release/business-verification)).
- **Sonuç:** İşletmenin kendi Meta uygulamasını açıp System User token'ını Vernuko'ya girdiği model satıcıya inceleme ya da sunucu yükü getirmez, ama kurulumu zordur. Tech Provider ve Embedded Signup modeli satıcı doğrulaması ve satıcı webhook altyapısı gerektirir, bu da sıfır maliyet hedefiyle çelişir.

### 1.6 Mesajlaşma limitleri ve throughput

- Limit, kayan 24 saatte pencere dışında ulaşılabilecek benzersiz kullanıcı sayısıdır ve portföy düzeyinde hesaplanır. Kademeler: 250 (başlangıç), 2.000, 10.000, 100.000 ve sınırsız. 2.000'e çıkmak için işletme doğrulanmalı ya da 30 günde yüksek kaliteli şablonlarla 2.000 teslim edilmiş mesaja ulaşılmalı. Sonraki geçişler otomatik (doğrulandı: [Messaging limits](https://developers.facebook.com/documentation/business-messaging/whatsapp/messaging-limits)). Tek şubeli bir klinik için 250 limiti genellikle yeterli.
- Throughput varsayılan olarak numara başına 80 mesaj/saniye, otomatik yükseltmeyle 1.000 mesaj/saniye (doğrulandı: [Throughput](https://developers.facebook.com/documentation/business-messaging/whatsapp/throughput/)).

### 1.7 On-Premises API'nin kapatılması

1 Temmuz 2024'ten beri numaralar yalnızca Cloud API'ye kaydediliyor. Son On-Premises sürümü (v2.63) 23 Ekim 2025'te sona erdi, bu numaraların mesajları artık teslim edilmiyor (doğrulandı: [On-Prem sunset](https://developers.facebook.com/docs/whatsapp/on-premises/sunset)). Yeni geliştirmede tek seçenek Cloud API.

### 1.8 Sağlık ve terapi randevuları: politika

- Politika, mevzuat sağlık bilgisinin yüksek güvenlik gerektiren sistemler dışında paylaşılmasını yasaklıyorsa WhatsApp'ın **telemedicine için ya da sağlık bilgisi göndermek veya istemek için** kullanılmasını yasaklıyor. Kart ve kimlik numarası gibi hassas tanımlayıcılar istenemez. Kullanıcıya ancak numarasını vermiş **ve** opt-in vermişse yazılabilir. Opt-out talepleri hangi kanaldan gelirse gelsin uygulanmalı (doğrulandı: [Business Messaging Policy](https://business.whatsapp.com/policy)).
- "Medical and healthcare products" kısıtlı ürünler arasında. Bu madde ürün satışı ve ticaret özellikleriyle ilgili, randevu hizmetini doğrudan kapsamıyor (doğrulandı: [Business Messaging Policy](https://business.whatsapp.com/policy), yorum bize ait).
- **Öneri:** Şablonda yalnızca tarih, saat, işletme adı ve iptal yolu bulunmalı. Terapi türü ya da tanı yazılmamalı. KVKK'daki özel nitelikli veri ve yurt dışına aktarım boyutu hukuki olarak ayrıca değerlendirilmeli (doğrulanamadı).

### 1.9 Manuel gönderim: click-to-chat

Bağlantı biçimi `https://wa.me/<numara>`. Numara uluslararası biçimde yazılır, başına sıfır, parantez ve tire konmaz. Önceden doldurulmuş mesaj için `https://wa.me/<numara>?text=<URL-encoded metin>` kullanılır (doğrulandı: [WhatsApp FAQ](https://faq.whatsapp.com/5913398998672934)). Türkiye için örnek: `https://wa.me/905321234567?text=...`. .NET'te `Uri.EscapeDataString` ile kodlanıp `Process.Start(new ProcessStartInfo(url){UseShellExecute=true})` ile açılır. Bu yöntem API ücreti doğurmaz, ancak durum bilgisi de vermez ve gönderim düğmesine kullanıcı basar.

---

## 2. Google Drive API (masaüstü)

### 2.1 Kurulu uygulamalar için OAuth 2.0

- Masaüstü uygulaması **sistem tarayıcısını** açmalı ve rastgele boş bir porttaki loopback adresini (`http://127.0.0.1:port` ya da `http://[::1]:port`) yönlendirme URI'si olarak vermeli. OOB (kopyala-yapıştır) yöntemi kaldırıldı. Masaüstü istemciler sır saklayamayan **public client** sayıldığı için PKCE her zaman kullanılmalı: S256 ve 43-128 karakterlik `code_verifier` (doğrulandı: [OAuth native apps](https://developers.google.com/identity/protocols/oauth2/native-app)).
- DPoP isteğe bağlı ama önerilen bir mekanizma: token'ları cihazdaki, tercihen TPM'de tutulan bir anahtara bağlar. Kurulu uygulamalara her zaman refresh token verilir, artımlı yetkilendirme desteklenmez (doğrulandı: [OAuth native apps](https://developers.google.com/identity/protocols/oauth2/native-app)).
- **Gömülü WebView yasak:** 30 Eylül 2021'den beri engelleniyor ve `disallowed_useragent` hatası dönüyor. WPF'teki WebView2 de bu kapsamda (doğrulandı: [Google Developers Blog](https://developers.googleblog.com/en/upcoming-security-changes-to-googles-oauth-20-authorization-endpoint-in-embedded-webviews/), [OAuth native apps](https://developers.google.com/identity/protocols/oauth2/native-app)).
- **"Testing" durumundaki** harici uygulamaların refresh token'ları 7 günde sona erer. Bir hesap ve istemci çifti için en fazla 100 refresh token tutulur, sınır aşılınca en eski token uyarısız geçersiz olur (doğrulandı: [OAuth 2.0 overview](https://developers.google.com/identity/protocols/oauth2)).

### 2.2 Kapsamlar: `drive.appdata` ve `drive.file`

- **Non-sensitive ve "çoğu kullanım için önerilen" kapsamlar:** `drive.appdata` (`drive.appfolder`), `drive.file` ve `drive.install`. **Restricted kapsamlar:** `drive`, `drive.readonly`, `drive.metadata` ve diğerleri. Non-sensitive kapsamlar yalnızca temel OAuth doğrulamasını gerektirir. Restricted kapsamlarda veri sunucuda saklanıyor ya da sunucu üzerinden aktarılıyorsa güvenlik değerlendirmesi zorunludur (doğrulandı: [Drive API scopes](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)).
- **Restricted kapsamın maliyeti:** Değerlendirme App Defense Alliance CASA çerçevesine göre Google'ın onayladığı bağımsız bir değerlendirici tarafından yapılır ve en az 12 ayda bir yenilenir. Süreç birkaç hafta sürebilir. Brand verification genellikle 2-3 iş günü sürer (doğrulandı: [Restricted scope verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/restricted-scope-verification)). Değerlendirme ücreti Google dokümanlarında yer almıyor (doğrulanamadı).
- **Doğrulanmamış uygulama sınırı:** Sensitive ya da restricted kapsam isteyen doğrulanmamış uygulamalar ömür boyu toplam 100 yeni kullanıcı ile sınırlıdır (doğrulandı: [Unverified apps](https://support.google.com/cloud/answer/7454865)).
- **appDataFolder:** Kullanıcıdan ve diğer uygulamalardan gizli bir klasördür. Kullanıcı uygulamayı Drive'dan kaldırdığında klasör silinir. Kullanıcı klasörü elle de silebilir (doğrulandı: [App data folder](https://developers.google.com/workspace/drive/api/guides/appdata)).
- **Karşılaştırma:** `drive.file` ile oluşturulan yedekler kullanıcının Drive'ında görünür ve indirilebilir. Bu, "veri işletmenindir" yaklaşımına ve elle kurtarmaya daha uygun. Uygulama aynı istemci kimliğiyle yalnızca kendi oluşturduğu dosyalara erişir (doğrulandı: kapsam tanımı, [Drive API scopes](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)). **Öneri:** Asıl yedekler için `drive.file`, küçük meta veri ve ayarlar için isteğe bağlı olarak `drive.appdata`.

### 2.3 Resumable upload

- Simple ve multipart yüklemeler 5 MB'a kadar dosyalar içindir. Resumable yükleme büyük dosyalar ve kesintiye açık ağlar için önerilir. Oturum URI'si **bir hafta** geçerlidir (doğrulandı: [Manage uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads)).
- Parçalar son parça hariç **256 KB'nin (256 × 1024 bayt) katı** olmalı. Her parça `Content-Range` başlığıyla PUT edilir. `308 Resume Incomplete` parçanın alındığını gösterir. Kesintiden sonra `Content-Range: bytes */<toplam>` ile durum sorgulanır ve yanıttaki `Range` başlığından devam edilir. `Range` başlığı yoksa hiç bayt alınmamıştır. `404` oturumun sona erdiğini gösterir ve yükleme baştan yapılır (doğrulandı: [Manage uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads)).

### 2.4 Kota

`about.get` yanıtındaki `storageQuota` nesnesi `limit`, `usage`, `usageInDrive` ve `usageInDriveTrash` alanlarını bayt cinsinden verir. Sınırsız depolamada `limit` gelmez, havuzlu kurumsal depolamada değerler kurumu gösterir (doğrulandı: [About resource](https://developers.google.com/workspace/drive/api/reference/rest/v3/about)). v3'te `fields=storageQuota` parametresinin zorunlu olduğu bilinen bir uygulama detayı (doğrulanamadı).

### 2.5 Hız limitleri ve satıcı projesine etkisi

- 1 Mayıs 2026'da yeni kotalar yürürlüğe girdi: proje başına dakikada 1.000.000 kota birimi, proje içinde kullanıcı başına dakikada 325.000 birim ve günde 1 TB egress. Maliyet birimleri: okuma 5, listeleme 100, düzenleme ve oluşturma 50, indirme 200 birim. Kota aşılırsa `403 User rate limit exceeded` ya da `429` döner ve exponential backoff uygulanmalıdır. Workspace kullanıcıları günde en fazla 750 GB yükleyebilir. Azami dosya boyutu 5 TB (doğrulandı: [Usage limits](https://developers.google.com/workspace/drive/api/guides/limits)).
- **Günlük faturalama eşiği:** Proje başına günde 400.000.000 kota birimi. Bu eşiğin üstü ücretlendirilecek, faturalama ayrıntıları 2026 içinde en az 90 gün önceden duyurulacak ve eşik artırılamıyor. Kasım 2025-Nisan 2026 arasında API'yi kullanmış projeler eski kotalarda kalıyor (doğrulandı: [Usage limits](https://developers.google.com/workspace/drive/api/guides/limits)). **Bu kota satıcının OAuth istemcisinin bağlı olduğu Cloud projesine yazılır.** Günlük bir yedeklemenin birkaç yüz birim tükettiği tahmin ediliyor, bu da binlerce müşteride bile eşiğin çok altında kalır (tahmin; resumable parça PUT'larının nasıl sayıldığı doğrulanamadı). Yine de satıcı için sıfır maliyet garantisi yok, bu risk takip edilmeli.

---

## 3. Microsoft OneDrive (Microsoft Graph)

### 3.1 Uygulama klasörü

`/drive/special/approot` uç noktası kullanıcının OneDrive'ında `Apps/<Uygulama adı>` klasörünü gösterir ve klasör ilk çağrıda oluşturulur. Uygulama `Files.ReadWrite.AppFolder` ya da `Files.ReadWrite` iznini istemelidir. Kullanıcı bu klasörü görebilir, içeriğini değiştirebilir, klasörü taşıyabilir ya da yeniden adlandırabilir. `approot:/{dosya}:/createUploadSession` desteklenir (doğrulandı: [OneDrive App Folder](https://learn.microsoft.com/en-us/onedrive/developer/rest-api/concepts/special-folders-appfolder)).

### 3.2 Kişisel hesaplar ve iş/okul hesapları: dokümanlar çelişiyor

- OneDrive izin referansına göre (2026-05-14) delegated `Files.ReadWrite.AppFolder` **yalnızca kişisel hesaplarda geçerli** ve "(preview)" olarak işaretli (doğrulandı: [OneDrive permissions](https://learn.microsoft.com/en-us/onedrive/developer/rest-api/concepts/permissions_reference)). Graph izin referansı da izni kişisel hesaplarda onaya açık olarak gösteriyor, Get special folder izin tablosu da onu yalnızca "personal Microsoft account" satırında listeliyor (doğrulandı: [Graph permissions](https://learn.microsoft.com/en-us/graph/permissions-reference), [Get special folder](https://learn.microsoft.com/en-us/graph/api/drive-get-specialfolder)).
- Buna karşılık Graph'ın app folder kılavuzu (2025-03-21) bu iznin "OneDrive for work or school ve OneDrive for home" üzerinde çalıştığını, application izniyle de kullanılabildiğini söylüyor (doğrulandı: [Graph app folder guide](https://learn.microsoft.com/en-us/graph/onedrive-sharepoint-appfolder)).
- **Öneri:** Kişisel hesaplarda AppFolder kullanılmalı. İş ve okul hesaplarında gerçek bir kiracıda test edilmeli, çalışmazsa daha geniş olan `Files.ReadWrite` iznine geçilmeli. Bir uyarı daha: risk tabanlı step-up consent açık olan kiracılarda, 8 Kasım 2020'den sonra kaydedilmiş ve publisher verification yapılmamış çok kiracılı uygulamalara kullanıcılar onay veremez. Publisher verification için bir Microsoft AI Cloud Partner Program hesabı gerekir (doğrulandı: [Publisher verification](https://learn.microsoft.com/en-us/entra/identity-platform/publisher-verification-overview)).

### 3.3 MSAL.NET ile masaüstü kimlik doğrulama

- Sistem tarayıcısı için `http://localhost` yönlendirme URI'si kaydedilmeli. MSAL `http://localhost:port` üzerinde dinler ve PKCE kullanır. Varsayılan ve önerilen seçenek sistem tarayıcısı. .NET 6+ Windows'ta gömülü görünüm `Microsoft.Identity.Client.Desktop` paketiyle mümkün ama "önerilmiyor" (doğrulandı: [MSAL web browsers](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers), [MSAL WAM](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam)).
- **WAM broker** Windows 10 1703 ve sonrasında çalışır. Yönlendirme URI'si `ms-appx-web://microsoft.aad.brokerplugin/{client_id}` olmalı. WAM kullanılamazsa MSAL otomatik olarak tarayıcıya döner. Yetki hem Entra ID'yi hem kişisel hesapları kapsıyorsa sistem hesap seçicisi gösterilir (doğrulandı: [MSAL WAM](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam)). WPF uygulaması için en iyi kullanıcı deneyimi WAM ile elde edilir.
- **Token önbelleği:** Masaüstü için `Microsoft.Identity.Client.Extensions.Msal` paketindeki çapraz platform önbellek öneriliyor. Paket, şifreleme başarısız olursa ACL korumalı düz metin moduna geçebiliyor. Özel önbellek örneği `ProtectedData.Protect(..., CurrentUser)` kullanıyor (doğrulandı: [Token cache serialization](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization)). Paketin Windows'ta içeride DPAPI kullandığı bu sayfada yazmıyor (doğrulanamadı).

### 3.4 Upload session

- `createUploadSession` bir `uploadUrl` döndürür. Parçalar **sırayla** PUT edilir ve tek bir istek 60 MiB'tan küçük olmalıdır. Parça boyutu **320 KiB'nin (327.680 bayt) katı olmak zorunda**, aksi halde commit hatası alınabilir. Önerilen parça boyutu 5-10 MiB. 10 MiB'tan büyük dosyalarda resumable yükleme önerilir (doğrulandı: [createUploadSession](https://learn.microsoft.com/en-us/graph/api/driveitem-createuploadsession)).
- PUT isteklerine **Authorization başlığı eklenmemeli**, eklenirse 401 dönebilir. Devam etmek için `nextExpectedRanges` alanı kullanılır. Zaten alınmış bir parça yeniden gönderilirse `416` döner. `404` oturumun kaybolduğunu gösterir ve yükleme baştan yapılır. Her parça `expirationDateTime` süresini uzatır. Ayrıca `conflictBehavior` (fail, replace ya da rename) ve `deferCommit` seçenekleri var. `fileSize` belirtilirse ve kotayı aşıyorsa `507` döner (doğrulandı: [createUploadSession](https://learn.microsoft.com/en-us/graph/api/driveitem-createuploadsession)).
- **Ortak parça boyutu:** 10 MiB hem 256 KiB'nin (40 katı) hem 320 KiB'nin (32 katı) tam katı olduğu için Google ve Microsoft'ta aynı boyut kullanılabilir. Dropbox'ın concurrent oturumu da işin içine girerse ortak kat 20 MiB olur (hesaplandı).

### 3.5 Kota

`drive.quota` nesnesi `total`, `used`, `remaining`, `deleted` ve `state` alanlarını içerir. `state` değerleri: `normal`, `nearing` (%10'dan az boş alan), `critical` (%1'den az) ve `exceeded`. `storagePlanInformation` alanı yalnızca kişisel OneDrive'da gelir (doğrulandı: [quota resource](https://learn.microsoft.com/en-us/graph/api/resources/quota)).

---

## 4. Dropbox API

### 4.1 Erişim türü ve scoped app

Uygulama oluşturulurken iki erişim türünden biri seçilir. **App Folder** yalnızca `/Apps/<uygulama>` klasörüne erişir. **Full Dropbox** kullanıcının bütün hesabına erişir. Eski uygulama türleri yeni uygulamalarda seçilemiyor. Erişim düzeyini scope'lar belirler (doğrulandı: [Dropbox OAuth guide](https://docs.dropboxapi.com/dropbox-api/docs/oauth), [DBX developer guide](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/developer-guide)).

### 4.2 PKCE ve refresh token

- Sunucusu olmayan masaüstü uygulamaları sırrı ikili dosyaya gömmek zorunda kaldıkları için PKCE kullanmalı. S256 önerilir ve `code_verifier` 43-128 karakter olmalı. Arka planda erişim gerektiren masaüstü uygulamalarında "code flow + PKCE + refresh token" kullanılır (doğrulandı: [Dropbox OAuth guide](https://docs.dropboxapi.com/dropbox-api/docs/oauth)).
- `token_access_type=offline` ile kısa ömürlü bir access token ve uzun ömürlü bir **refresh token** alınır. Refresh token kendiliğinden sona ermez. Uzun ömürlü access token'lar kullanımdan kaldırıldı (doğrulandı: [Dropbox authorization](https://docs.dropboxapi.com/dropbox-api/docs/get-started/authorization)).
- Yönlendirme URI'si **birebir kayıtlı olmalı**, localhost dahil. localhost dışındaki URI'ler HTTPS olmalı. Code flow'da yönlendirme URI'si zorunlu değil: verilmezse kod kullanıcıya gösterilir ve kullanıcı bunu uygulamaya yapıştırır. Yetkilendirme sayfası sistem tarayıcısında açılmalı, **WebView'da açılmamalı** (doğrulandı: [Dropbox authorization](https://docs.dropboxapi.com/dropbox-api/docs/get-started/authorization)). Rastgele loopback portu için port eşleşmesinin esnek olup olmadığı doğrulanamadı. Bu yüzden önceden kayıtlı birkaç sabit port kullanılmalı ya da kod yapıştırma akışı yedek olarak tutulmalı.

### 4.3 Production onayı

Development durumundaki bir uygulamaya en fazla 500 kullanıcı bağlanabilir. Ancak **50 kullanıcıya ulaşıldıktan sonra iki hafta içinde production onayı alınmazsa** yeni kullanıcı bağlanması dondurulur. Başvuruda marka kurallarına uyum ve gerekenden geniş izin istenmemesi değerlendirilir (doğrulandı: [DBX developer guide](https://docs.dropboxapi.com/dropbox-api/docs/developer-resources/developer-guide)).

### 4.4 Upload session

- `/files/upload` 150 MB'tan küçük dosyalar içindir. Daha büyük dosyalarda `upload_session/start`, `append_v2` ve `finish` (ya da batch varyantları) kullanılır. Performans kılavuzu 4 MB'ın katı parçalar ve en fazla 1.000 öğelik batch'ler öneriyor (doğrulandı: [Dropbox performance](https://docs.dropboxapi.com/dropbox-api/docs/performance)).
- Tek bir istek 150 MiB'ı geçmemeli. Bir oturumla yüklenebilecek azami dosya boyutu 2^41 − 2^22 bayt (yaklaşık 2 TiB). Oturum **en fazla 7 gün** geçerli, sonrasında `not_found` döner. `session_type: concurrent` kullanılırsa veri yalnızca append çağrılarıyla gönderilebilir ve son `close=true` çağrısı hariç her append **4.194.304 baytın katı** olmalı. Bu çağrılar, aylık limiti olan Business ekiplerinde "data transport" çağrısı sayılır (doğrulandı: [upload_session/start](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/files/upload-session-start)). Sıralı oturumda devam etmek için `cursor.offset` kullanılır. `incorrect_offset` hatasının doğru offset'i döndürüp döndürmediği doğrulanamadı.

### 4.5 Alan kullanımı

`users/get_space_usage` uç noktası `used` alanını ve `allocation` nesnesini (individual ya da team) döndürür. Gerekli scope `account_info.read` (doğrulandı: [get_space_usage](https://docs.dropboxapi.com/dropbox-api/api-reference/user-endpoints/users/get-space-usage)). App Folder uygulamasında bu scope ayrıca istenmeli.

### 4.6 Resmi .NET SDK

`Dropbox.Api` paketinin son sürümleri: 7.4.0 (23.09.2026), 7.3.0 (16.09.2026), 7.2.0 (14.07.2026). Bundan önceki sürüm 7.0.0, 22.05.2024 tarihli. Paket .NET Standard 2.0'ı hedefliyor, `Newtonsoft.Json >= 13.0.4` bağımlılığı var, sahibi `dropbox`, toplam indirme yaklaşık 9,3 milyon (doğrulandı: [NuGet Dropbox.Api](https://www.nuget.org/packages/Dropbox.Api)). İki yıllık aradan sonra 2026'da yeniden düzenli sürüm çıkıyor. Depodaki LICENSE MIT, nuspec'te lisans alanı yok (doğrulandı: [LICENSE](https://github.com/dropbox/dropbox-sdk-dotnet/blob/main/LICENSE)). GitHub etkinliği incelenemedi (doğrulanamadı). Birkaç uç noktayı doğrudan `HttpClient` ile çağırmak da makul bir alternatif.

---

## 5. İstemci tarafında şifreli yedekleme

### 5.1 AES-256-GCM sınırları

- GCM'de aynı anahtar ve aynı IV'nin iki farklı girdiyle kullanılma olasılığı 2^-32'yi geçmemeli. Rastgele IV kullanılan yapıda bir anahtarla en fazla **2^32 şifreleme çağrısı** yapılabilir. Tek bir çağrıda düz metin 2^39 − 256 biti (yaklaşık 64 GiB) geçemez (doğrulandı: [NIST SP 800-38D](https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-38d.pdf)).
- .NET'teki `AesGcm` yalnızca tek seferlik `Encrypt` ve `Decrypt` sunar, akış API'si yoktur. Etiket boyutu almayan kurucular `Obsolete` (doğrulandı: [AesGcm](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm)). Dolayısıyla parçalı şifreleme zaten gerekli.

### 5.2 Parçalı AEAD: STREAM yapısı

- Hoang, Reyhanitabar, Rogaway ve Vizár'ın OAE2 tanımı ve STREAM yapısı parçalı AEAD için temel referanstır. Makaleye göre hiçbir OAE tanımı nonce tekrarını anlamlı biçimde tolere edemez (doğrulandı: [ePrint 2015/189](https://eprint.iacr.org/2015/189)).
- **Tink AES-GCM-HKDF Streaming** (HRRV15'e dayanır) somut bir referans tasarım sunar. Başlık `len ‖ Salt ‖ NoncePrefix(7 bayt)` biçimindedir. Dosya anahtarı `HKDF(ikm=anahtar, salt=Salt, info=AssociatedData)` ile türetilir. Her parçanın IV'si `NoncePrefix ‖ i (4 bayt, big-endian) ‖ b` biçimindedir ve **b son parçada 0x01, diğerlerinde 0x00'dır.** En fazla 2^32 parça olabilir (doğrulandı: [Tink AES-GCM-HKDF Streaming](https://developers.google.com/tink/streaming-aead/aes_gcm_hkdf_streaming)). Parça indeksi sıra değiştirmeyi, son parça bayrağı ise kesilmeyi (truncation) engeller.
- **Vernuko için önerilen biçim** (tasarım önerisi):
  - Başlıkta magic, sürüm, KDF parametreleri, sarılmış DEK, salt ve nonce prefix yer alır.
  - Her yedek için rastgele, yeni bir 256 bit DEK üretilir. Böylece nonce benzersizliği kendiliğinden sağlanır.
  - Düz metin 1 MiB'lık parçalara bölünür. Her parçanın nonce'u `prefix ‖ i ‖ last` biçimindedir ve AAD olarak `SHA-256(başlık)` kullanılır. Bu, başlığın değiştirilmesini de engeller.
  - Geri yüklemede çözülen parçalar geçici bir dosyaya yazılır. Veritabanı ancak son parça doğrulandıktan sonra yerine konur, çünkü kesilme ancak sonda fark edilir.

### 5.3 Anahtar sarma ve hiyerarşi

- DEK, KEK ile sarılır. KEK bir kurtarma parolasından ya da yazdırılabilir bir kurtarma anahtarından türetilir. .NET 10, `Aes` sınıfına **AES Key Wrap with Padding (RFC 5649)** desteği ekledi (`EncryptKeyWrapPadded` ve `DecryptKeyWrapPadded`) (doğrulandı: [What's new in .NET 10 libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)). Alternatif olarak DEK, AES-GCM ile de sarılabilir.
- Gözetimsiz otomatik yedekleme için KEK'in (ya da türetilmiş anahtarın) yerel kopyası DPAPI ile korunur. Böylece parola yalnızca kurulumda ve geri yüklemede sorulur.

### 5.4 Anahtar türetme (KDF)

- **OWASP:** PBKDF2-HMAC-SHA256 için **600.000 iterasyon**, PBKDF2-HMAC-SHA512 için 220.000. Argon2id için asgari yapılandırma **m = 19 MiB, t = 2, p = 1**. Eşdeğer seçenekler: 46 MiB/t=1, 12 MiB/t=3, 9 MiB/t=4, 7 MiB/t=5 (doğrulandı: [OWASP Password Storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)).
- **RFC 9106:** Birinci önerilen ayar Argon2id t=1, p=4, m=2 GiB. Bellek kısıtlı ortamlar için ikinci önerilen ayar Argon2id t=3, p=4, m=64 MiB. İkisinde de 128 bit salt ve 256 bit etiket kullanılır (doğrulandı: [RFC 9106](https://www.rfc-editor.org/rfc/rfc9106.html)).
- .NET'te `Rfc2898DeriveBytes.Pbkdf2` statik metodu yerleşik olarak var (doğrulandı: [Rfc2898DeriveBytes.Pbkdf2](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes.pbkdf2)). .NET 10'un yenilikler sayfasında Argon2 geçmiyor, bu yüzden üçüncü taraf bir kütüphane, örneğin libsodium bağlaması gerekir (doğrulanamadı).
- **Öneri:** Bir masaüstü uygulamasında birkaç saniyede bir yapılan anahtar türetmesi için RFC 9106'nın ikinci ayarı (64 MiB, t=3, p=4) uygun. Bağımlılık eklenmeyecekse PBKDF2-SHA256 ile 600.000 iterasyon kullanılabilir. Parametreler başlıkta tutulmalı ki sonradan yükseltilebilsin.

### 5.5 Windows DPAPI (ProtectedData)

`ProtectedData` Windows'taki DPAPI'nin sarmalayıcısıdır ve **yalnızca Windows'ta** çalışır, diğer platformlarda `PlatformNotSupportedException` fırlatır. `DataProtectionScope.CurrentUser` seçilirse veriyi yalnızca aynı kullanıcı çözebilir. İsteğe bağlı bir entropy parametresi verilebilir (doğrulandı: [ProtectedData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata), [How to use data protection](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection)). **Sınırlama:** Anahtar kullanıcı hesabına ve makineye bağlı, cihaz kaybolursa anahtar da kaybolur. Bu yüzden DPAPI yedeğin tek kökü olmamalı, kurtarma anahtarı zorunlu tutulmalı. Aynı kullanıcı olarak çalışan bir zararlı yazılım veriyi çözebilir (doğrulanamadı, genel tehdit modeli).

### 5.6 Tutarlı SQLite yedeği

- **`VACUUM INTO 'dosya'`** çalışan veritabanının tutarlı bir anlık görüntüsünü üretir. Çıktı minimum boyuttadır ve silinmiş içerik temizlendiği için adli iz kalmaz, bu sağlık verisi için bir avantaj. Hedef dosya ya hiç var olmamalı ya da boş olmalı. İşlem yarıda kesilirse çıktı bozuk olabilir. `synchronous` ayarı NORMAL ya da FULL ise çıktı fsync ile diske yazılır. Backup API ise daha az CPU harcar ve artımlı çalışabilir (doğrulandı: [VACUUM](https://sqlite.org/lang_vacuum.html)).
- **Backup API:** Kaynak başka bir bağlantı tarafından değiştirilirse yedekleme bir sonraki `sqlite3_backup_step()` çağrısında baştan başlar (doğrulandı: [sqlite3_backup](https://sqlite.org/c3ref/backup_finish.html)). `Microsoft.Data.Sqlite` paketindeki `SqliteConnection.BackupDatabase` metodu en hızlı yolu seçer ve işlem süresince diğer bağlantıların yazmasını engeller (doğrulandı: [Microsoft.Data.Sqlite backup](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup)).
- **WAL:** WAL dosyası veritabanının kalıcı durumunun bir parçasıdır. Yalnızca `.db` dosyası kopyalanırsa commit edilmiş işlemler kaybolabilir ya da dosya bozulabilir (doğrulandı: [WAL](https://sqlite.org/wal.html)). Bu yüzden dosya kopyalama yöntemi kullanılmamalı.
- **Önerilen akış:** `VACUUM INTO` ile geçici bir dosyaya yazılır. Ardından `PRAGMA integrity_check` çalıştırılır, dosya STREAM-GCM ile şifrelenir, resumable olarak yüklenir ve yükleme sonrası boyut ile hash doğrulanır. En son geçici dosya güvenli biçimde silinir.

---

## 6. SMS segment hesabı (GSM 03.38 / 3GPP TS 23.038)

### 6.1 Temel kapasiteler

- 7 bit varsayılan alfabe: 140 oktette 160 karakter. UCS-2: 70 karakter. Genişletme tablosundaki her karakter, kaçış (escape) karakteriyle birlikte 2 septet tutar (doğrulandı: [TS 23.038 v18.0.0](https://www.etsi.org/deliver/etsi_ts/123000_123099/123038/18.00.00_60/ts_123038v180000p.pdf)). Genişletme tablosundaki karakterler: `| ^ € { } [ ~ ] \` ve sayfa sonu.
- Birleştirilmiş (concatenated) iletide parça başına kapasite 7 bitte **153**, UCS-2'de **67** karakter. Kaçış dizisi iki parçaya bölünemez. En fazla 255 parça olabilir (doğrulandı: [TS 23.040 v18.0.0 §9.2.3.24.1](https://www.etsi.org/deliver/etsi_ts/123000_123099/123040/18.00.00_60/ts_123040v180000p.pdf)).

### 6.2 Türkçe karakterler ve ulusal dil tabloları

- **Varsayılan alfabede olanlar:** Ç, Ö, ö, Ü, ü. **Olmayanlar:** ç (küçük), ğ, Ğ, ı, İ, ş, Ş (doğrulandı: TS 23.038 §6.2.1 tablosu). Bu karakterlerden biri bile varsa, ulusal dil göstergesi kullanılmadığında ileti UCS-2'ye düşer.
- **Turkish National Language Single Shift Table (A.2.1)** varsayılan genişletme tablosunun yerine geçer ve Ş, ç, ş, Ğ, ğ, İ, ı, € karakterleriyle varsayılan genişletme karakterlerini içerir. Her biri kaçış ile birlikte 2 septet tutar (doğrulandı: TS 23.038 A.2.1).
- **Turkish Locking Shift Table (A.3.1)** temel tablonun yerine geçer. İ, ç, €, ı, Ğ, ğ, Ş, ş karakterleri tek septet olur. Buna karşılık è, ì, Ø, ø, Æ, æ, ¡ ve ¿ karakterleri tablodan çıkar (doğrulandı: TS 23.038 A.3.1).
- **UDH bilgi öğeleri:** Single shift için IEI 0x24, locking shift için IEI 0x25. İkisinin de veri uzunluğu 1 oktet, yani her biri UDH'de toplam 3 oktet tutar. UDH septet sınırında bitmezse dolgu bitleri eklenir (doğrulandı: TS 23.040 §9.2.3.24, §9.2.3.24.15-16). Bilgi öğesi **her parçada tekrarlanmalı**, eksik olduğu parça varsayılan genişletme tablosuyla çözülür (doğrulandı: TS 23.040 §9.2.3.24.15).
- **Kapasite:** Tek iletide single shift ile 4 oktet (5 septet) gider ve kapasite **155** olur. Locking ve single shift birlikte kullanılırsa 7 oktet (8 septet) gider ve kapasite **152** olur (doğrulandı: TS 23.038 Ek C). Birleştirilmiş iletide UDH = 1 + 5 + 3 = 9 oktet = 72 bit, yani 11 septet eder ve parça başına **149** kalır. İki bilgi öğesi birlikte kullanılırsa UDH 12 oktet (14 septet) olur ve parça başına **146** kalır (hesaplandı, TS 23.040 dolgu kuralına göre).
- **Geri uyumluluk:** Single shift'i desteklemeyen cihazlar bilgi öğesini yok sayar. Ek C'deki örnekte "Türkçe" kelimesi "Türkce" olarak görünür. Locking shift'i desteklemeyen cihazlar ise metni varsayılan tabloyla çözer ve örneğin ı yerine ì gösterir (doğrulandı: TS 23.038 Ek C; ì örneği tablolardan çıkarım).

### 6.3 Örnek hesap (hesaplandı)

Örnek metin (122 karakter): "Sayın Ayşe Hanım, 14.11.2026 Cuma 14:30 randevunuzu hatırlatırız. İptal/erteleme için lütfen bizi arayın. Dr. Çelik Klinik"

| Kodlama | Uzunluk | Parça |
|---|---|---|
| Varsayılan GSM | kodlanamaz | UCS-2'ye düşer: 122 birim, **2 parça** (67'lik) |
| Türkçe single shift | 131 septet | **1 parça** (≤155) |
| Türkçe locking shift | 122 septet | 1 parça (≤155) |
| Türkçe karakterler ASCII'ye çevrilirse | 122 septet | 1 parça (≤160) |

Vernuko'daki sayaç, kullanıcının seçtiği SMS sağlayıcısının ulusal dil tablolarını destekleyip desteklemediğine göre bu üç modu ayrı ayrı hesaplamalı. Türk operatörlerinin ve toplu SMS sağlayıcılarının API'lerinin bu tabloları desteklediği doğrulanamadı. Emoji ve BMP dışındaki karakterler UTF-16'da 2 birim tutar. İYS (İleti Yönetim Sistemi) yükümlülükleri bu araştırmanın kapsamı dışında (doğrulanamadı).

---

## 7. Önerilen kararlar

| Konu | Öneri |
|---|---|
| WhatsApp v1 | wa.me ile manuel gönderim ve isteğe bağlı olarak "işletmenin kendi Meta uygulaması ile System User token" üzerinden Cloud API ile gönderim. Durum bilgisi için B seçeneği (işletmenin kendi aktarıcısı) kurulum sihirbazıyla sunulabilir. Utility şablonu içerik olarak sade tutulmalı. |
| WhatsApp maliyeti | Türkiye'de utility mesajı başına yaklaşık 0,0009 USD. İşletmenin hesabında ödeme yöntemi zorunlu, para birimi USD ya da EUR. |
| Google Drive | Kapsam `drive.file`. Sistem tarayıcısı, loopback ve PKCE. Uygulama "In production" durumuna alınmalı, aksi halde refresh token'lar 7 gün sürer. Parça boyutu 10 MiB. |
| OneDrive | WAM ve `Files.ReadWrite.AppFolder` (kişisel hesaplar). İş ve okul hesaplarında test edilmeli, gerekirse `Files.ReadWrite`. Parça boyutu 10 MiB (320 KiB'nin katı). |
| Dropbox | App Folder, PKCE ve offline erişim. Sabit loopback portları kaydedilmeli. 50 kullanıcıdan önce production başvurusu yapılmalı. |
| Yedek biçimi | `VACUUM INTO`, `integrity_check`, STREAM-GCM şifreleme (1 MiB parçalar, indeks ve son parça bayrağı), DEK'in AES-KWP ile sarılması, Argon2id (64 MiB, t=3, p=4) ya da PBKDF2 (600 bin), DPAPI yalnızca yerel önbellek için. |
| SMS sayacı | GSM-7, Türkçe single shift (155/149) ve UCS-2 (70/67) modları. Sağlayıcı profiline göre seçilmeli. |

---

## Kaynaklar

Tüm kaynaklar ilgili iddianın yanında satır içinde verilmiştir. Hepsi resmi dokümantasyon, standart (3GPP/ETSI, NIST) ya da RFC'dir. ePrint 2015/189 ve OWASP birincil akademik ve topluluk standardı kaynaklarıdır.
