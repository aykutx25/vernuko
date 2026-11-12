> **Araştırma notu.** Bu rapor 11–12 Kasım 2026'da resmi web siteleri, yardım merkezleri, fiyat sayfaları ve herkese açık kullanıcı yorumları taranarak hazırlanmıştır. Her iddia kaynağıyla birlikte "doğrulandı" / "doğrulanamadı" olarak işaretlenmiştir. Fiyatlar ve politikalar sık değişir; satın alma veya hukuki karar öncesinde ilgili kaynak yeniden kontrol edilmelidir. Hukuki konular avukat görüşü yerine geçmez.

# Vernuko: Türkiye Pazarı ve Mevzuat Araştırması

Tarih: 12 Kasım 2026. Kapsam: Türk rakipler (A) ve Türkiye'ye özgü mevzuat/entegrasyon gerçekleri (B).

> **Hukuki tavsiye değildir.** Mevzuat bölümleri resmi metinlerin özetidir. "⚖️ Avukat" işaretli maddeler bir KVKK/bilişim avukatına, "🧾 Mali müşavir" işaretliler bir mali müşavire sorulmalıdır.

**İşaretleme:** Her iddianın yanında `(doğrulandı: [Kod])` var. Kod, sondaki kaynak listesindeki URL'dir ve sayfa bu araştırmada okunmuştur. `(doğrulanamadı)` resmi ya da birincil bir kaynakta görülemeyen bilgiyi gösterir. `(yorum)` benim çıkarımımdır. Fiyatlar sayfada görüldüğü haliyle verilmiştir. KDV durumu sayfada yazmıyorsa ayrıca belirtilmiştir.

**Erişim kısıtları:** Şikayetvar otomatik erişime 403 döndürdü. Bu yüzden şikayetler yalnızca arama motoru özetlerinden ve sayfa başlıklarından alındı. Akınsoft siteleri de 403 döndürdü. iys.org.tr içeriği JavaScript ile yüklendiği için okunamadı. Google Play sayfaları açılamadı, bu yüzden yalnızca App Store yorumlarına bakıldı.

---

## 0. Karar için en önemli 10 bulgu

1. **Pazardaki ürünlerin neredeyse tamamı bulut tabanlı (web + mobil).** Aylık fiyatlar 700 ₺ ile 4.000 ₺ arasında. Masaüstü rakipler eski tip, tek seferlik lisanslı ürünler (17.999–21.000 ₺). Bunların içinde "offline-first + müşteriye ait yedek" modelini açıkça sunan tek örnek diş hekimliğindeki ücretsiz **Diş Hekimi Pro**. (doğrulandı: [C2][C7][C12][C18][C19][C21])
2. **"Kendi SMS hesabını getir" (BYO) modeli yaygın.** Arvengo SMS'i salonun NETGSM hesabından gönderiyor. SalonJet kampanya SMS'lerini salonun kendi sağlayıcı hesabından gönderiyor. Diş Hekimi Pro kullanıcının kendi Netgsm/Twilio hesabını bağlıyor. (doğrulandı: [C9][C2][C21]) Vernuko v1 için de BYO en az riskli yol (yorum).
3. **Aracı firmalardan alınan SMS kredisi yaklaşık 2 kat pahalı.** Salon Randevu'nun App Store'daki uygulama içi fiyatı 500 SMS için 399,99 ₺. Netgsm'de 500 SMS 198 ₺, İleti Merkezi'nde 189 ₺ (KDV+ÖİV dahil). (doğrulandı: [C14][S2][S5])
4. **Randevu hatırlatma için İYS onayı gerekmez, ama içerikte tanıtım olamaz.** Yönetmelik md. 6/1-2, sağlanan hizmetle ilgili bildirimler için onay aramıyor ve İYS kontrolü de yapılmıyor (md. 6/5). Bu tür bildirimlerde "herhangi bir mal veya hizmet özendirilemez" (doğrulandı: [I1]). Yönetmelik "randevu" kelimesini açıkça kullanmıyor; hatırlatmanın bu kapsama girdiği bir yorumdur. ⚖️ Avukat
5. **Doğum günü mesajı ticari iletidir.** md. 5/1 "kutlama ve temenni gibi içeriklerle tanınırlığını artırmak" amacıyla gönderilen iletileri ticari sayıyor. Bu yüzden onay ve İYS kaydı gerekir (doğrulandı: [I1]). Rakiplerin çoğu doğum günü SMS'ini temel özellik olarak satıyor (doğrulandı: [C9][C5]).
6. **Google Drive/OneDrive'a otomatik yedek KVKK'da yurt dışına aktarımdır.** KVKK rehberi, yurt dışındaki bir bulutta depolamayı açıkça aktarım sayıyor (doğrulandı: [L4]). Düzenli yedekleme "arızi" sayılamaz (doğrulandı: [L4]). Bu durumda standart sözleşme gerekir ve imzadan itibaren **5 iş günü** içinde Kurum'a bildirilmelidir (doğrulandı: [L1]). Tüketici Google veya Microsoft hesapları için böyle bir sözleşmenin imzalanabildiğini doğrulayamadım. ⚖️ Avukat (kritik)
7. **VERBİS'te sağlık alanı için yeni bir eşik var.** Ana faaliyeti özel nitelikli veri işlemek olanlar ancak <10 çalışan ve <10 milyon ₺ bilanço koşuluyla muaf. Bilanço esasına göre defter tutmayanlarda yalnızca çalışan sayısına bakılıyor (Kurul kararı 2025/1572). Avukatlar ve SMMM/YMM ayrıca muaf. (doğrulandı: [L6][L7])
8. **Kâğıt fatura dönemi bitiyor.** e-Arşiv'e dahil olmayanların faturaları 1/1/2026'dan itibaren tutara bakılmaksızın e-Arşiv olmalı. Basit usul ve işletme hesabı esasında defter tutanlar için bu tarih 1/1/2027. Serbest meslek erbabı 2020'den beri e-SMM düzenlemek zorunda. (doğrulandı: [E1]) Vernuko'nun v1'de resmi belge "basmaması" gerekiyor. Entegratör entegrasyonu ise bir yol haritası fırsatı (yorum).
9. **Sağlık verisinde risk yüksek ve hukuk tartışmalı.** Muayenehanelerden MBYS/e-Nabız üzerinden veri gönderimi bekleniyor. Bunun cezası, 3359 sayılı Kanun Ek-11 uyarınca önceki ayın brüt hizmet gelirinin %2'si (en az 50 bin ₺) olarak aktarılıyor (doğrulandı: [H2], meslek örgütü notu). HBYS üreticileri Bakanlık "Kayıt ve Tescil" sistemine kayıtlı (doğrulandı: [H1]). Vernuko v1'in tıbbi kayıt sistemi gibi konumlanmaması gerekir (yorum). ⚖️ Avukat
10. **Masaüstü yazılım şikayetleri belirli bir örüntü izliyor.** "Lisans süresi bitince sistem kilitlendi" ve "program açılmıyor, 3 gündür erişemiyorum" gibi şikayetler var (doğrulandı: [C24][C25], yalnızca başlıklar). Bulut salon yazılımlarında ise "fiyatlar birden arttı" ve "WhatsApp bağlantısı kopuyor" öne çıkıyor (doğrulandı: [C16]; [C15] yalnızca arama özeti).

---

## A. Türk rakipler

### A.1 Özet tablo

| Ürün | Segment | Platform | Fiyat (sayfadaki) | SMS modeli |
|---|---|---|---|---|
| **SalonJet** | Kuaför, berber, spa, güzellik | Web (app.salonjet.com), mobil uyumlu | Ücretsiz plan; Pro 700 ₺/ay veya 7.000 ₺/yıl; KDV belirtilmemiş (doğrulandı: [C2]) | Kampanya SMS'i salonun kendi sağlayıcısından; randevu onay/hatırlatma SalonJet WhatsApp hattından ücretsiz (doğrulandı: [C2]) |
| **Salun** (Kirpi Soft) | Kuaför, güzellik, klinik, diyetisyen, spa, dövme | Web + mobil (doğrulandı: [C3]) | Tanışma 0 ₺; Yükselme kutuda 1.600 ₺/ay, Zirve 4.000 ₺/ay, üst şeritte ise 479/799 ₺, yani **tutarsız**; yıllıkta %20 indirim (doğrulandı: [C4]) | Pakete dahil 150 veya 500 SMS/ay; aşım ücreti yazmıyor (doğrulandı: [C4]) |
| **Rezzim** | Berber, kuaför, tırnak, güzellik; cilt, epilasyon, pilates, solaryum | iOS/Android + web panel + Apple Watch (doğrulandı: [C6]) | Solo 1.499 ₺/ay (14.999 ₺/yıl), Salon 1.999 (19.999), Pro 2.999 (29.999); **KDV dahil** (doğrulandı: [C7]) | "Otomatik SMS hatırlatma" her pakette var; kota ve ücret (doğrulanamadı) |
| **Arvengo** | Kuaför, güzellik, tırnak, berber, akademi | Web + iOS/Android (doğrulandı: [C8]) | HTML'deki değerler: aylık 1.090/1.490/1.990, yıllık 11.400/15.600/20.400. Para birimi "USD" etiketli ama büyük olasılıkla TL (doğrulanamadı). KDV ifadeleri çelişkili (doğrulandı: [C9]) | SMS NETGSM'den, salon adıyla gidiyor; hat ve SMS bedeli NETGSM'ye ödeniyor, pakete dahil değil (doğrulandı: [C9]) |
| **Salon Randevu** (Vatan Software) | Güzellik, kuaför, spa, klinik vb. | Web + mobil (doğrulandı: [C11]) | Gold 1.590, Platinum 2.400, Diamond 3.480 ₺/ay; ilk alımda indirimli; ek personel ve her eklenti 225 ₺/ay (doğrulandı: [C12]) | Kendi SMS kredisi (500–20.000 adet) (doğrulandı: [C13]); App Store'da 500 SMS 399,99 ₺ (doğrulandı: [C14]) |
| **SalonAppy / Kolay Randevu** | Salon | Mobil + web | App Store uygulama içi: 1.499–3.249,99 ₺ (doğrulandı: [C16]) | (doğrulanamadı) |
| **Orest** (masaüstü) | Güzellik/estetik | Masaüstü (doğrulandı: [C18]) | 17.999 ₺ (önceki fiyat 21.999 ₺); KDV ve lisans modeli belirtilmemiş (doğrulandı: [C18]) | SMS var, ücret modeli (doğrulanamadı) |
| **CosmeticSoft** (Indemsoft) | Güzellik/estetik, lazer | Masaüstü, ağda çoklu PC (doğrulandı: [C19]) | 21.000 ₺, süresiz lisans; ilk yıl güncelleme ve destek ücretsiz (doğrulandı: [C19]) | SMS Mutlucell adına veriliyor, faturayı Mutlucell kesiyor (doğrulandı: [C19]) |
| **Hursoft** | Güzellik merkezi | Windows, SQL veritabanı (doğrulandı: [C20]) | Fiyat yok (doğrulandı: [C20]) | SMS var, ücret (doğrulanamadı) |
| **Diş Hekimi Pro** | Diş | Windows masaüstü, offline, LAN modu (doğrulandı: [C21]) | Temel kullanım ücretsiz; bulut yedek ve kontör ücretli (doğrulandı: [C21]) | Kendi Netgsm/Twilio/SMTP hesabı ücretsiz veya kendi bulut kontörü (doğrulandı: [C21]) |
| **DentalBulut** | Diş | Bulut (doğrulandı: [C22]) | SSS: 1 hekim 960 ₺/ay ya da 7.200 ₺/yıl, ek hekim 320 ₺/ay; plan tablosu bununla tutarsız (doğrulandı: [C22]) | SMS ücreti yazmıyor (doğrulandı: [C22]) |
| **GymKod** | Pilates, yoga, PT stüdyosu | Bulut web + mobil (doğrulandı: [C23]) | 1.000 ₺+KDV/ay, 10.000 ₺+KDV/yıl; ilk 3 ay ücretsiz (doğrulandı: [C23]) | Sayfada SMS bilgisi yok (doğrulandı: [C23]) |
| **Akınsoft ProKuaför/ProCoiffeur** | Kuaför, güzellik, epilasyon | (doğrulanamadı; site 403) | (doğrulanamadı) | (doğrulanamadı) |

Arama özetlerinde başka pilates ve diş yazılımları da çıktı (Bugifit, Stuvio, BenimSeansım, AppointSoft, Dentsy, selfDent). Fiyatları resmi sayfadan okunmadığı için (doğrulanamadı).

### A.2 Ürün notları

**SalonJet.** Ücretsiz plan 1 personel, ayda 50 randevu, 10 fiş, 30 WhatsApp mesajı ve son 30 günün raporlarıyla sınırlı. Pro planda bu sınırlar kalkıyor (doğrulandı: [C2]). Seans paketleri ve personel komisyonu yalnızca Pro'da (doğrulandı: [C1]). Doğum günü ve yorum isteği mesajları "yalnızca pazarlama izni veren müşterilere" gidiyor. Ürün, ticari ve operasyonel mesaj ayrımını uyguluyor (doğrulandı: [C2]). Pro'da onam formları ve CSV dışa aktarma da var (doğrulandı: [C2]). Ana sayfa özeti SMS'in "Netgsm hesabı" üzerinden gittiğini söylüyor, fiyat sayfası ise sağlayıcı adı vermiyor (doğrulanamadı). Verinin nerede saklandığı yazmıyor (doğrulandı: [C2]).

**Salun.** Modül listesi geniş: Instagram DM (SalunDM), resmi WhatsApp API (SalunWP), e-fatura (SalunFatura), fiziki POS, online kapora (SalunPAY), dijital onam (KVKK) ve rol bazlı yetki (doğrulandı: [C3][C4]). E-fatura, fiziki POS, dijital onam ve çoklu şube yalnızca en üst paket olan Zirve'de var (doğrulandı: [C4]). Hatırlatma SMS'i 1 gün veya 2 saat önce gidiyor. İptal olduğunda sıradaki kişiye haber veriliyor, toplu kampanya SMS'i de gönderilebiliyor (doğrulandı: [C5]). İYS'den hiç söz edilmiyor (doğrulandı: [C4][C5]).

**Rezzim.** Bütün özellikler her pakette aynı, paketleri yalnızca personel sayısı ayırıyor. Instagram DM otomasyonu 499 ₺/ay'lık ek modül ve henüz "yakında" durumunda (doğrulandı: [C7]). Müşteri kartında gelmeme sayısı, toplam harcama ve doğum günü tutuluyor. Kasa nakit, kart ve havaleyi ayrı gösteriyor. Prim cirodan otomatik hesaplanıyor, yetkiler rol ve kişi bazında veriliyor. Yapay zekâ asistanı sesle randevu ekleyebiliyor (doğrulandı: [C6][C7]). Fiyatlar 7 dilde, KDV dahil ve tutarlı olarak yayımlanmış. Pazardaki en şeffaf fiyat sayfası bu (doğrulandı: [C7]).

**Arvengo.** Paketlerde personel sınırı 2, 8 ve 10. **Paket/seans takibi, prim ve stok yalnızca "Full" pakette** (doğrulandı: [C9]). İkinci şubede %50 indirim var. SMS paketleri ay ay bitirme zorunluluğu olmadan alınıyor ama yıl sonunda sıfırlanıyor (doğrulandı: [C9]). App Store'da 4,6 puan ve 11 değerlendirme var, görünen yorumların hepsi olumlu (doğrulandı: [C10]).

**Salon Randevu.** Prim, paket ve stok üst paketlerde (doğrulandı: [C11]). App Store'daki abonelik fiyatları web fiyatlarından yüksek: Gold 2.249,99 ₺/ay, web'de 1.590 ₺ (doğrulandı: [C12][C14]). Uygulamada 4,7 puan ve 265 değerlendirme var (doğrulandı: [C14]). Web sayfasının SSS bölümünde Gold için 99 ₺/ay yazıyor. Bu, fiyat tablosuyla çelişiyor (doğrulandı: [C11]).

**Masaüstü rakipler** (Orest, CosmeticSoft, Hursoft). Hepsinde klasik özellikler var: seans bazlı paket, kasa/POS, prim ve bonus, SMS hatırlatma, doğum günü ve toplu SMS (doğrulandı: [C18][C19][C20]). Hursoft kabin/cihaz uygunluğu ile patron, resepsiyon ve uzman için ayrı menü yetkileri sunuyor ve Windows 98–11'de çalıştığını belirtiyor (doğrulandı: [C20]). Hiçbiri offline çalışmayı veya şifreli bulut yedeği açıkça anlatmıyor. CosmeticSoft yalnızca "manuel/otomatik yedek" ve isteğe bağlı "bulut sunucu" seçeneğinden söz ediyor (doğrulandı: [C19]).

**Diş Hekimi Pro.** Vernuko'nun modeline en yakın ürün. Windows 10/11'de çalışıyor. Hasta verisi bilgisayarda kalıyor ve "Diş Hekimi Pro sunucularına gönderilmiyor". LAN modunda tek veritabanı paylaşılabiliyor. Bulut yedekte şifreleme cihazda yapılıyor. Gelir isteğe bağlı bulut yedek ve kontör satışından geliyor (doğrulandı: [C21]). Sitede e-Nabız entegrasyonundan söz edilmiyor (doğrulandı: [C21]).

### A.3 Tekrarlayan şikayetler

- **Ani fiyat artışı.** SalonAppy için App Store'da "Fiyatlar birden bire aşırı arttı" yorumu var (doğrulandı: [C16]). Salonrandevu.com için "kısa sürede yüksek üyelik artışı" şikayeti var (arama özeti; Şikayetvar sayfası açılamadı: [C15], doğrulanamadı).
- **WhatsApp entegrasyonunun kopması.** Salonrandevu.com'da bir güncellemeden sonra bağlantının sürekli koptuğu ve hatırlatmalar gitmediği için boşluklar oluştuğu bildirilmiş ([C15], doğrulanamadı).
- **Ödeme sonrası fatura alamama.** EFT ile ödeme yapıldıktan sonra fatura gelmediği şikayeti var ([C15], doğrulanamadı).
- **Mesaj özelliklerinin "yasal uyum" gerekçesiyle kısıtlanması.** SalonAppy'de müşteri geri bildirim mesajları yalnızca online randevulara gönderilir hale getirildi. Geliştirici bunu mevzuata uyum gerekçesiyle açıkladı (doğrulandı: [C16]).
- **Masaüstü ve lisanslı yazılım.** "Lisans süresi bitince sistem kilitlendi" (Logo) ve "program açılmıyor, 3 gündür sisteme erişemiyorum" (Bilsoft) gibi başlıklar var (doğrulandı: [C24][C25], yalnızca başlık). Yenileme dönemlerindeki fiyat artışları da sık görülüyor (arama özeti, doğrulanamadı).
- **Fiyat şeffaflığı.** Salun, Arvengo, Salon Randevu ve DentalBulut sayfalarında aynı paket için birbiriyle çelişen fiyat veya vergi ifadeleri var (doğrulandı: [C4][C9][C11][C22]).
- Rezzim, SalonJet ve Arvengo hakkında kamuya açık bir şikayet bulamadım. Bu, sorun olmadığını göstermez. Bu ürünler küçük ve yeni olabilir (yorum).

### A.4 Vernuko için ürün çıkarımları (yorum)

- **Konumlama.** Rakip kümesi şöyle: bulut SaaS (700–4.000 ₺/ay), eski masaüstü (17–21 bin ₺ tek sefer) ve dişte ücretsiz offline ürün. Vernuko'nun vaadi "verin sizde, internet gitse de çalışır, kilitlenmez" olabilir. Lisans bittiğinde salt-okunur moda geçmek ve veriyi dışa aktarmayı her zaman açık tutmak, doğrudan [C24]'teki şikayete cevap verir.
- **Temel özellikler** (bütün rakiplerde var): takvim, müşteri kartı, seans/paket, kasa (nakit/kart/havale), prim, SMS hatırlatma ve rapor. **Fark yaratabilecekler:** şeffaf ve KDV dahil fiyat (Rezzim örneği), mesaj türünü bilgilendirme/ticari olarak ayıran şablon motoru (SalonJet örneği) ve KVKK araçları (onam, aydınlatma, imha kayıtları).
- **SMS.** v1'de BYO olmalı: müşteri kendi Netgsm, İleti Merkezi veya Verimor hesabını bağlar. Vernuko SMS satmaz. Böylece başlık (originator) ve İYS sorumluluğu işletmede kalır. Vernuko da aracı hizmet sağlayıcı ya da veri işleyen rolüne girmez. ⚖️ Avukat

---

## B. Mevzuat ve entegrasyon

### B.1 KVKK (6698 sayılı Kanun)

**Özel nitelikli veri.** Sağlık, biyometrik ve genetik veriler, kılık-kıyafet ve ceza mahkûmiyeti gibi veriler özel niteliklidir. 7499 sayılı Kanunla (2/3/2024) md. 6/3 yeniden yazıldı. İşleme yasak, istisnaları ise şunlar: açık rıza, kanunda öngörülme, bir hakkın tesisi, ve "sır saklama yükümlülüğü altındaki kişiler"in tıbbi teşhis ve tedavi amacıyla işlemesi. Ayrıca Kurul'un belirlediği **yeterli önlemler** şart (md. 6/4). (doğrulandı: [L1])
- *Yorum:* Güzellik ve lazer merkezlerindeki cilt tipi, alerji, gebelik ve ilaç gibi anamnez verileri sağlık verisidir. Bu işletmeler tıbbi teşhis istisnasına muhtemelen dayanamaz, bu yüzden açık rıza gerekebilir. Hekim, diş hekimi ve psikolog için md. 6/3-e istisnası değerlendirilebilir. ⚖️ Avukat
- **Fotoğraf.** KVKK'nın özel nitelikli veri rehberine göre fotoğraf tek başına biyometrik veri değildir. Kişiyi benzersiz biçimde tanıyan özel bir teknikle işlendiğinde biyometrik veri olur (arama özeti: [L10], doğrulanamadı). Öncesi/sonrası fotoğrafları ise sağlık durumunu gösterebilir (yorum). ⚖️ Avukat
- **Spor salonu.** Kurul, üye girişinin biyometrik veriyle kontrolü konusunda karar vermiş (doğrulandı: [L11], yalnızca başlık). Vernuko parmak izi veya yüz tanıma entegrasyonu yapmamalıdır (yorum).

**Yeterli önlemler (Kurul Kararı 2018/10) ürün gereksinimine dönüşüyor.** Karar şunları istiyor: verinin kriptografik yöntemlerle şifrelenmesi, anahtarların ayrı ortamda saklanması, verideki tüm işlemlerin güvenli biçimde loglanması, yazılımda kullanıcı yetkilendirmesi ve düzenli güvenlik testi, uzaktan erişimde en az iki kademeli kimlik doğrulama, taşınabilir medyaya şifreli aktarım, e-posta için KEP veya şifreleme, sunucular arası aktarımda VPN veya sFTP. (doğrulandı: [L5]) Vernuko için bunun karşılığı şifreli SQLite veya şifreli yedek, denetim kaydı (audit log), rol bazlı yetki ve şifreli USB/bulut yedektir (yorum).

**Yurt dışına aktarım (7499 sonrası md. 9).**
- Aktarım üç yoldan biriyle yapılabilir: (1) yeterlilik kararı, (2) uygun güvenceler, (3) arızi istisnalar. Uygun güvencelerden biri, **Kurul'un ilan ettiği standart sözleşme**dir. Standart sözleşme **imzadan itibaren 5 iş günü içinde Kurum'a bildirilir**. (doğrulandı: [L1])
- Standart sözleşmenin metni değiştirilemez, yalnızca seçimli maddeler doldurulur. İki taraf da Türkçe metni imzalar ve imza tarihlerini yazar. Bildirim fiziki olarak, KEP ile veya Bildirim Modülü üzerinden yapılır. (doğrulandı: [L2])
- Arızi aktarım (örneğin açık rıza) yalnızca "tek seferlik veya birkaç kez" yapılan ve süreklilik göstermeyen aktarımlar için kullanılabilir. **Olağan faaliyet akışındaki aktarımlar istisnai sayılamaz.** (doğrulandı: [L4])
- Rehbere göre yurt dışındaki bir bulutta depolama ve yurt dışından ekran üzerinden destek amaçlı uzaktan erişim de "aktarım" sayılır. (doğrulandı: [L4])
- Bildirim yapmamanın cezası 2026 için 90.308–1.806.177 ₺. (doğrulandı: [L9])

**Google Drive/OneDrive'da yedek: kim veri sorumlusu?**
- Veri sorumlusu, işleme amaç ve vasıtalarını belirleyen kişidir. Veri işleyen ise onun adına veri işleyendir (doğrulandı: [L1] md. 3). Veri sorumlusu, kendi adına veri işleyenle birlikte veri güvenliğinden **müştereken sorumludur** (doğrulandı: [L1] md. 12/2).
- *Yorum:* Rollerin dağılımı şöyle görünüyor:
  - **İşletme** (salon veya klinik) veri sorumlusudur.
  - **Google veya Microsoft**, depolama hizmeti veren veri işleyendir. Sunucular yurt dışındaysa bu bir yurt dışı aktarımıdır.
  - **Vernuko** veriye hiç erişmiyorsa (yerel veritabanı, müşterinin kendi bulut hesabı, telemetride kişisel veri yok) büyük olasılıkla ne veri sorumlusu ne veri işleyendir.
  - Vernuko uzaktan destekte ekran görürse veya hata raporlarında kişisel veri toplarsa veri işleyen olur. Bu erişim yurt dışından yapılıyorsa ayrıca aktarım doğar (bkz. [L4]).
- **Açık sorun.** Kişisel (tüketici) Google veya Microsoft hesaplarıyla KVKK standart sözleşmesinin imzalanabildiğini doğrulayamadım. Google Cloud'un KVKK gizlilik bildirimini anan ikincil bir kaynak var, Türkçe standart sözleşme metni bulunamadı (doğrulanamadı). Önerilen v1 yaklaşımı (yorum):
  1. Varsayılan yedek yerel veya NAS'a gitsin.
  2. Bulut yedek isteğe bağlı olsun ve uyarı metniyle açılsın.
  3. Yedek istemci tarafında şifrelensin ve anahtar yalnızca işletmede kalsın. Şifrelemenin aktarım niteliğini ortadan kaldırıp kaldırmadığını rehberde göremedim. ⚖️ Avukat (kritik)
  4. İleride Türkiye'de konumlu bir depolama seçeneği eklensin.

**VERBİS.**
- Muafiyetler: yalnızca otomatik olmayan yollarla veri işleyenler, noterler, avukatlar, arabulucular, SMMM ve YMM'ler. Ayrıca <50 çalışan ve <100 milyon ₺ bilançosu olup ana faaliyeti özel nitelikli veri işlemek olmayanlar. **Ana faaliyeti özel nitelikli veri işlemek olup <10 çalışan ve <10 milyon ₺ bilançosu olanlar da muaf** (2025/1572; RG 01.10.2025). (doğrulandı: [L6])
- "Ana faaliyet" tespitinde ticaret sicili veya vergi levhasındaki NACE kodlarına bakılıyor (doğrulandı: [L6] Soru 79).
- Bilanço esasına göre defter tutmayanlar için yalnızca çalışan sayısı ölçüt alınıyor (doğrulandı: [L7]).
- *Yorum:* Çoğu salon ve spor salonu muaf görünüyor. 10 ve üzeri çalışanı olan klinik, diş ve psikoloji merkezleri kayıt yükümlüsü olabilir.
- Muafiyet yalnızca kayıt yükümlülüğünü kaldırır, Kanun'un diğer hükümleri geçerli kalır (doğrulandı: [L6]).
- VERBİS ihlali cezası 2026 için 341.809–17.092.242 ₺ (doğrulandı: [L9]).

**Saklama ve imha** (Yönetmelik, RG 28.10.2017).
- Sicile kayıtlı veri sorumluları saklama ve imha politikası hazırlar.
- Periyodik imha aralığı **en fazla 6 ay**. Politikası olmayanlar imhayı 3 ay içinde yapar.
- İmha işlemleri loglanır ve loglar **en az 3 yıl** saklanır.
- İlgili kişinin talebi **30 gün** içinde sonuçlandırılır.
(doğrulandı: [L8])

Ürün karşılığı (yorum): müşteri bazında "sil / anonimleştir" işlevi, imha logu, saklama süresi kuralları ve toplu periyodik imha raporu.

**2026 ceza aralıkları:** aydınlatma yükümlülüğü 85.437–1.709.200 ₺, veri güvenliği 256.357–17.092.242 ₺ (doğrulandı: [L9]).

### B.2 İYS ve 6563 sayılı Kanun (Ticari Elektronik İleti)

Kaynak: Ticari İletişim ve Ticari Elektronik İletiler Hakkında Yönetmelik (RG 15.07.2015; 4/1/2020'de değişti). (doğrulandı: [I1])

**Ticari ileti.** Mal ve hizmeti tanıtmak, pazarlamak veya "kutlama ve temenni" yoluyla tanınırlık artırmak amacıyla gönderilen iletidir. Önceden onay gerekir. Gönderen İYS'ye kaydolur. **İYS'de onayı olmayan alıcıya ticari ileti gönderilemez** (md. 5).

**Onay gerektirmeyen iletiler (md. 6):**
- Alıcının iletişim bilgisini kendisi verdiği durumlarda, sağlanan mal veya hizmetin değişikliği, kullanımı ve bakımıyla ilgili iletiler (6/1).
- Devam eden abonelik veya üyelik, tahsilat, borç hatırlatma, bilgi güncelleme ve teslimat bildirimleri (6/2). **Bu bildirimlerde hiçbir mal veya hizmet özendirilemez ya da tanıtılamaz.**
- Bu kapsamdaki gönderimlerde İYS kontrolü yapılmaz (6/5). Aracı hizmet sağlayıcı, gönderenden bu yönde bir beyan alır (md. 11/8). SMS API'lerindeki "bilgilendirme" bayrağı bu beyandır (bkz. B.3).
- Tacir ve esnaf alıcılara onay gerekmez, ama numaralar İYS'ye kaydedilir ve ret durumu kontrol edilir (6/3, 6/6).

**Vernuko şablonları için sınıflandırma** (yorum):
- **Bilgilendirme:** randevu onayı, hatırlatma, erteleme, iptal, "paketinizde 2 seans kaldı" ve ödeme hatırlatması.
- **Ticari:** doğum günü kutlaması, kampanya, "yeni hizmetimiz", hatırlatma metnine eklenen indirim cümlesi ve "geri gelin" mesajları.
- **Belirsiz:** yorum veya değerlendirme isteği. ⚖️ Avukat

**İşletmenin (hizmet sağlayıcı) yükümlülükleri:**
- Onay kaydı ad, soyad, iletişim adresi ve olumlu irade beyanını içerir. Önceden işaretli kutu kullanılamaz. Onay, hizmetin ön şartı yapılamaz. Onay talebi ticari iletiyle gönderilemez (md. 7/1, 7/4, 7/8, 7/9).
- İYS dışında alınan onaylar **3 iş günü** içinde İYS'ye kaydedilir. Kaydedilmeyen onay geçersizdir (7/11-12).
- Ticari SMS'te tacir için MERSİS numarası, esnaf için ad-soyad ve TCKN/VKN yer alır (md. 8/3). **Her iletide** ücretsiz ret imkânı bulunur (md. 9/3-4).
- Ret bildirimi 3 iş günü içinde İYS'ye iletilir ve gönderim durdurulur (md. 9/6, 10/1).
- Onay kayıtları ve diğer kayıtlar 3 yıl saklanır. İspat yükü gönderendedir (md. 13).

**SMS sağlayıcısının (aracı hizmet sağlayıcı) yükümlülükleri:**
- Sistemini İYS ile uyumlu hale getirir.
- İYS'ye kayıtlı olmayan hizmet sağlayıcının ticari iletisini başlatmaz.
- Gönderimden önce her alıcının İYS onayını kontrol eder.
- Başkası adına onay alamaz.
(md. 11/4-7)

**Diğer notlar:**
- İletişim adresi 250 binden az olan işletmelerin İYS'ye yalnızca entegratör üzerinden erişebildiği ikincil bir kaynakta geçiyor (doğrulanamadı: [I2]).
- WhatsApp mesajlarının da "ticari elektronik ileti" sayılacağı tanımdaki "kısa mesaj hizmeti **gibi** vasıtalar" ifadesinden çıkarılabilir (yorum). ⚖️ Avukat

### B.3 SMS sağlayıcıları

| | **Netgsm** | **İleti Merkezi** | **Verimor** |
|---|---|---|---|
| API | REST v2 JSON (`POST /sms/rest/v2/send`); XML POST (`/sms/send/xml`) da var. **HTTP GET ile gönderim 2022'den beri desteklenmiyor** (doğrulandı: [S1]) | REST JSON; bütün uçlar POST, `/v1/…/json` (doğrulandı: [S3]) | HTTP GET (`/v2/send`) ve JSON POST (`/v2/send.json`) (doğrulandı: [S6]) |
| Kimlik | HTTP Basic: abone no + "API kullanıcısı" alt kullanıcı şifresi (15.02.2024'ten beri zorunlu); IP kısıtı isteğe bağlı (doğrulandı: [S1]) | Gövdede `key` + `hash`; hash'i panel hazır üretir; paneldeki "API kullanımına izin ver" açık olmalı; statik IP whitelist isteğe bağlı ama üretim için önerilir (doğrulandı: [S3]) | `username` + API şifresi; **gönderim yapan sunucunun IP'sini panele girmek zorunlu (BTK)**, aksi halde 401 (doğrulandı: [S6]) |
| DLR | `POST /sms/rest/v2/report` ile jobid veya tarih aralığına göre sorgu; durumlar: 0 bekliyor, 1 iletildi, 2 zaman aşımı, 3 hatalı numara, 11 operatör reddi, 13 mükerrer vb. SMS için push DLR görmedim; İYS için webhook var (doğrulandı: [S1]) | `get-report` / `get-reports` + webhook (hesap başına tek URL, **imzasız**) (doğrulandı: [S3]) | PUSH (panelde tanımlı URL'ye JSON POST) veya `GET /v2/status`; rapor sorgusu 20/dk ile sınırlı (doğrulandı: [S6]) |
| Türkçe ve segment | `encoding`: UTF-8 / TR / UNICODE. TR'de ç ğ ı ş Ğ İ Ş 2 karakter sayılıyor. BTK kararıyla eklenen operatör kodu (B021) yüzünden başlıklı SMS'te 1 boy 155 karakter. Mesaj başına üst sınır: UTF-8 912, TR 883, UNICODE 391. `/sms/rest/v2/length` ile boy hesabı yapılabiliyor (doğrulandı: [S1]) | GSM-7 155/153, Türkçe 150/148, UCS-2 65/63; en fazla 7 boy (~1071 karakter) (doğrulandı: [S4][S5]) | `datacoding` 0/1/2. 1 boy: 160 / 155 / 70; 7 boy: 1071 / 1043 / 469. Yalnızca Ş ş Ğ ğ ç ı İ Türkçe sayılıyor; Ö ö Ü ü Ç normal kodlamayla gidiyor (doğrulandı: [S6]) |
| Başlık | `msgheader` 3–11 karakter ve hesapta tanımlı olmalı (doğrulandı: [S1]) | `sender` en fazla 11 karakter ve onaylı olmalı; belgeler KEP ile gönderiliyor, onay 1–2 iş günü; test için `APITEST` (doğrulandı: [S3][S4]) | `source_addr`; boş bırakılırsa ilk kayıtlı başlık kullanılıyor (doğrulandı: [S6]) |
| İYS | `iysfilter`: "0" bilgilendirme, "11" bireysele ticari, "12" tacire ticari; `iys/add`, `iys/search`, İYS webhook ve kara liste uçları (doğrulandı: [S1]) | `iys` "1"/"0", `iysList` BIREYSEL/TACIR; `consent/create`, `consent/show`; kara liste uçları (doğrulandı: [S3][S4]) | `is_commercial`, `iys_recipient_type` BIREYSEL/TACIR; `iys_consents.json`; kara liste uçları (doğrulandı: [S6]) |
| Fiyat (KDV+ÖİV dahil) | 500 SMS 198 ₺, 1.000 SMS 370 ₺, 5.000 SMS 1.089 ₺, 10.000 SMS 1.699 ₺; yeni aboneye 10.000 SMS 899 ₺; paket 1 yıl geçerli, iade yok; paket bitince "mevcut tarife" uygulanıyor (doğrulandı: [S2]) | **Ön ödemeli.** 500 SMS 189 ₺, 1.000 SMS 359 ₺, 5.000 SMS 1.049 ₺, 10.000 SMS 1.599 ₺; yeni aboneye 10.000 SMS 879 ₺; 1 yıl geçerli; **ulaşmayan SMS ücretlendirilmiyor**; yeni hesaba 100 ücretsiz kredi (doğrulandı: [S5][S3]) | **Ön ödemeli.** Yıllık paket: 500 SMS 199 ₺, 1.000 SMS 369 ₺, 5.000 SMS 1.099 ₺, 10.000 SMS 1.699 ₺; aylık paketler ay sonunda bitiyor; yalnızca teslim edilen SMS düşüyor (doğrulandı: [S7]) |

Uyarı: GSM-7'de 160/153, UCS-2'de 70/67 genel standart değerleridir (doğrulanamadı; bu araştırmada bir standart metni okunmadı). **Türk sağlayıcıların boy hesabı birbirinden ve bu standarttan farklı** (doğrulandı: [S1][S4][S6]). Bu yüzden boy ve maliyet sağlayıcıya özel hesaplanmalı ya da sağlayıcının hesaplama API'sinden alınmalı (yorum).

Masaüstü ürün için önemli bir not (yorum): Verimor'da IP whitelist zorunlu, İleti Merkezi de statik IP öneriyor. Dinamik IP'li bir salon bilgisayarı bu yüzden kesinti yaşayabilir. Vernuko'nun bir aktarma (relay) sunucusu kullanması ise onu veri yoluna sokar ve veri işleyen yapar. v1'de en az sürtünmeli seçenek Netgsm (IP kısıtı isteğe bağlı) veya İleti Merkezi. SMS gönderilemediğinde zamanlanmış kuyruk ve yeniden deneme şart.

### B.4 e-Fatura, e-Arşiv ve e-SMM

- **e-Fatura zorunluluğu.** 2022 ve sonraki dönemlerde brüt satış hasılatı **3 milyon ₺ ve üzeri** olanlar e-Fatura'ya geçmek zorunda. İnternetten satış yapanlar için eşik 500 bin ₺. (doğrulandı: [E1]) Eşiği aşanlar izleyen yılın 7. ayının başında geçiyor (doğrulandı: [E1], arama özeti üzerinden).
- **e-Arşiv.** e-Arşiv'e dahil olmayan mükellefler için kurallar şöyle:
  - 2025'te vergiler dahil toplam tutarı 3 bin ₺'yi aşan faturalar e-Arşiv olmak zorundaydı.
  - **1/1/2026'dan itibaren tutara bakılmaksızın** bütün faturalar e-Arşiv olmalı.
  - Basit usul ve işletme hesabı esasında defter tutanlar için bu tarih **1/1/2027** (589 Sıra No.lu Tebliğ ile).
  - e-Arşiv fatura GİB portalından veya **GİB'den izin almış özel entegratör** sistemlerinden düzenlenir. Kâğıt düzenlenirse VUK md. 353 cezası uygulanır.
  (doğrulandı: [E1])
- **e-SMM.** Vergiden muaf olmayan serbest meslek erbabı 1/6/2020'den beri e-Serbest Meslek Makbuzu düzenlemek zorunda (doğrulandı: [E1]). *Yorum:* Muayenehanedeki hekim ve diş hekimi, psikolog, avukat ve mali müşavir bu kapsamda.
- **Vernuko v1 neden resmi belge düzenlememeli** (yorum): Resmi belge, mali mühür veya nitelikli elektronik sertifika ile imzalanır ve GİB ya da entegratör üzerinden düzenlenir (doğrulandı: [E1], tanımlar). Uygulamanın "fatura" görünümlü bir kâğıt basması müşteriyi md. 353 riskine sokar. Sağlanacak olan: "mali değeri yoktur" ibareli adisyon veya bilgi fişi, muhasebeye veya entegratöre aktarım için CSV/Excel çıktısı ve fatura numarası ya da ETTN girme alanı. 🧾 Mali müşavir (ÖKC/yazar kasa fişiyle ilişkisi doğrulanamadı)
- **Sonraki aşamada entegratör entegrasyonu** (yorum): GİB'in özel entegratör listesindeki bir firmanın API'si kullanılır (liste: [E2], içerik okunmadı). Gereken işler:
  - alıcının e-Fatura mükellefi olup olmadığının sorgulanması, ardından e-Fatura veya e-Arşiv seçimi;
  - GİB formatında XML oluşturma (UBL-TR, doğrulanamadı);
  - gönderim, durum takibi, iptal/itiraz ve PDF görüntü;
  - e-SMM desteği;
  - müşteri başına entegratör hesabı ve kimlik bilgisinin güvenli saklanması.
  
  Offline-first bir uygulamada belgeler kuyruğa alınır ve bağlantı gelince gönderilir. Bekleyen belge durumu arayüzde açıkça gösterilmelidir.

### B.5 Sağlık sektörü

- **Veri gönderim yükümlülüğü.** Bir sektör kaynağına göre Bakanlık resmi yazısı özel muayenehanelerden, ağız ve diş sağlığı muayenehanelerinden ve polikliniklerden işlem verilerini Bakanlık standartlarında merkezi sisteme göndermelerini istiyor. Dayanak olarak şunlar gösteriliyor: 3359 sayılı Kanun md. 3 ve Ek-11, Kişisel Sağlık Verileri Yönetmeliği md. 21/3 ve Ağız ve Diş Sağlığı Hizmeti Sunulan Özel Sağlık Kuruluşları Yönetmeliği md. 22/3. (doğrulandı: [H3], ikincil kaynak; resmi yazı görülmedi)
- **Hukuki durum tartışmalı.** Türk Dermatoloji Derneği notuna göre:
  - 15 Ocak 2025'te 7538 sayılı Kanunla 3359'a Ek-19 eklendi. Bu düzenleme, AYM'nin 1 sayılı CBK md. 378'i iptal etmesinin ardından geldi.
  - Yeni bir yönetmelik çıkmadan MBYS genelgesine dayanarak veri istenmesinin hukuka aykırı olabileceği savunuluyor.
  - Yine de Ek-11'e dayalı "**en az 50 bin ₺, önceki ay brüt hizmet gelirinin %2'si**" tutarında ceza riski bulunuyor.
  
  (doğrulandı: [H2], meslek örgütü görüşü) Kişisel Sağlık Verileri Yönetmeliği'nde 2025 ve 2026'da değişiklikler yayımlanmış (doğrulanamadı: [H4], içerik okunmadı). ⚖️ Avukat
- **Yazılım tescili.** Sağlık Bakanlığı "Kayıt ve Tescil Birimi" HBYS, AHBS ve diğer yazılım üreticilerini kaydediyor. 2017'de bütün HBYS ve AHBS üreticilerinden USS entegrasyonu istendi. (doğrulandı: [H1], arama özeti) Güncel başvuru şartları (doğrulanamadı). ÜTS (Ürün Takip Sistemi) bu araştırmada incelenmedi (doğrulanamadı).
- **Vernuko için öneri** (yorum):
  - v1'de hekim, diş hekimi ve psikolog segmentine "randevu ve ön büro" olarak girilmeli. Tanı, tedavi, reçete ve klinik not tutan bir "hasta dosyası" olarak konumlanılmamalı.
  - e-Nabız/MBYS entegrasyonu vaat edilmemeli. Müşteriye, Bakanlık yükümlülüklerini MBYS veya tescilli bir yazılımla ayrıca yerine getirmesi gerektiği söylenmeli.
  - Serbest not alanları için "sağlık verisi girmeyin" uyarısı ya da alan bazında şifreleme ve erişim logu düşünülmeli.
  - Güzellik ve lazer merkezindeki anamnez formu için açık rıza akışı ve 2018/10'daki önlemler uygulanmalı.
  - Veteriner segmentinde hayvanın sağlık verisi kişisel veri değildir, sahibinin bilgileri ise kişisel veridir (yorum).

---

## C. Avukata ve mali müşavire sorulacaklar

1. ⚖️ Müşteriye ait Google Drive/OneDrive yedeği için standart sözleşme gerçekten imzalanabilir mi? İstemci tarafı şifreleme aktarım niteliğini değiştirir mi? Uygulamadaki uyarı metni ne demeli?
2. ⚖️ Vernuko'nun rolü nedir? Telemetri, uzaktan destek veya isteğe bağlı aktarma sunucusu olduğunda veri işleyen sözleşmesi gerekir mi?
3. ⚖️ Randevu hatırlatma ve "kalan seans" bildirimleri md. 6/1-2 kapsamına giriyor mu? Yorum isteği ve "seansınız bitti, yenileyin" mesajları nasıl sınıflanır? WhatsApp 6563 kapsamında mı?
4. ⚖️ Güzellik ve lazer merkezleri anamnez ve fotoğraf için hangi hukuki sebebe dayanır? Açık rıza metni nasıl olmalı?
5. ⚖️ Hekim, diş hekimi ve psikolog müşteriler için MBYS/e-Nabız yükümlülüğü ve "tıbbi kayıt yazılımı" sayılma riski nedir?
6. 🧾 Adisyon veya bilgi fişinin hukuki niteliği, ÖKC (yazar kasa) ile ilişkisi ve entegratör seçimi.

---

## Kaynaklar

**Rakipler**
- [C1] SalonJet ana sayfa: https://salonjet.com.tr/
- [C2] SalonJet fiyatlandırma: https://salonjet.com.tr/fiyatlandirma
- [C3] Salun ana sayfa: https://salun.com.tr/
- [C4] Salun paketler: https://salun.com.tr/paketlerimiz
- [C5] Salun otomatik bildirimler: https://salun.com.tr/ozellikler/otomatik-bildirimler
- [C6] Rezzim: https://rezzim.com/
- [C7] Rezzim fiyatlandırma: https://rezzim.com/fiyatlandirma
- [C8] Arvengo: https://arvengo.com/
- [C9] Arvengo fiyatlar: https://arvengo.com/fiyatlar
- [C10] Arvengo App Store: https://apps.apple.com/tr/app/arvengo/id6738986183?l=tr
- [C11] Salon Randevu işletmeler: https://salonrandevu.com/isletmeler-icin
- [C12] Salon Randevu fiyatlar: https://salonrandevu.com/randevu-takip-programi-fiyatlari
- [C13] Salon Randevu SMS paketleri: https://salonrandevu.com/sms-paketleri
- [C14] Salon Randevu App Store: https://apps.apple.com/tr/app/id1413695973?l=tr
- [C15] Şikayetvar Salonrandevu.com (403; yalnızca arama özeti): https://www.sikayetvar.com/salonrandevucom
- [C16] SalonAppy App Store: https://apps.apple.com/tr/app/id1097504667?l=tr
- [C17] Şikayetvar Kolay Randevu (arama özeti): https://www.sikayetvar.com/kolay-randevu
- [C18] Orest masaüstü: https://www.orestteknoloji.com/urun/guzellik-ve-estetik-merkezleri-programi-masaustu
- [C19] Indemsoft CosmeticSoft: https://www.indemsoft.com/programlar/12/guzellik-ve-estetik-merkezleri.html
- [C20] Hursoft: https://www.hursoft.net/urun/guzellik-merkezi-programi
- [C21] Diş Hekimi Pro: https://dishekimipro.com/
- [C22] DentalBulut fiyatlar: https://www.dentalbulut.com/en/prices
- [C23] GymKod: https://gymkod.com/
- [C24] Şikayetvar Logo, "lisans süresi bitince sistem kilitlendi" (başlık): https://www.sikayetvar.com/logo-yazilim/lisans-suresi-bitince-sistem-kilitlendi-90-gunluk-tolerans-ihlali
- [C25] Şikayetvar Bilsoft, "program açılmıyor" (başlık): https://www.sikayetvar.com/bilsoft-yazilim/bilsoft-yazilim-program-acilmiyor-ve-3-gundur-hicbir-sekilde-sisteme-erisemiyorum
- [C26] Akınsoft ProKuaför katalog (403): https://www.akinsoft.com.tr/programlar/e-katalog/pdf/prokuafor_2021.pdf

**KVKK**
- [L1] 6698 sayılı Kanun, güncel metin: https://www.mevzuat.gov.tr/mevzuatmetin/1.5.6698.pdf
- [L2] KVKK, standart sözleşmelerde dikkat edilecek hususlar (05.02.2025): https://www.kvkk.gov.tr/Icerik/8170/Yurt-Disina-Kisisel-Veri-Aktariminda-Kullanilacak-Standart-Sozlesmelerde-Dikkat-Edilmesi-Gereken-Hususlara-Iliskin-Kamuoyu-Duyurusu
- [L4] Kişisel Verilerin Yurt Dışına Aktarılması Rehberi: https://kvkk.gov.tr/Icerik/8142/Kisisel-Verilerin-Yurt-Disina-Aktarilmasi-Rehberi
- [L5] Kurul Kararı 2018/10, yeterli önlemler: https://www.kvkk.gov.tr/Icerik/4110/2018-10
- [L6] Sorularla VERBİS: https://www.kvkk.gov.tr/SharedFolderServer/CMSFiles/2f2fbcf4-932b-4dad-af8a-cd32e315d815.pdf
- [L7] 2025/1572 uygulama esasları duyurusu (12.01.2026): https://www.kvkk.gov.tr/Icerik/8577/kisisel-verileri-koruma-kurulunun-04-09-2025-tarihli-ve-2025-1572-sayili-kararinin-uygulama-esaslarina-iliskin-kamuoyu-duyurusu
- [L8] Silme, Yok Etme veya Anonim Hale Getirme Yönetmeliği (RG 28.10.2017): https://www.resmigazete.gov.tr/eskiler/2017/10/20171028-10.htm
- [L9] KVKK idari para cezası tablosu 2017–2026: https://www.kvkk.gov.tr/SharedFolderServer/CMSFiles/MTY5NTUyNTRkNDkxNGY.pdf
- [L10] Özel Nitelikli Kişisel Verilerin İşlenmesine İlişkin Rehber (arama özeti): https://www.kvkk.gov.tr/SharedFolderServer/CMSFiles/70f95c73-06a2-44dc-81e9-34201bdd7f5c.pdf
- [L11] Spor salonunda biyometrik giriş kararı 2019/81-165 (başlık): https://www.kvkk.gov.tr/Icerik/5496/2019-81-165

**İYS ve SMS**
- [I1] Ticari İletişim ve Ticari Elektronik İletiler Hakkında Yönetmelik: https://www.mevzuat.gov.tr/mevzuat?MevzuatNo=20914&MevzuatTur=7&MevzuatTertip=5
- [I2] İYS entegratör eşiği (ikincil kaynak): https://ikas.com/tr/blog/iys-ileti-yonetim-sistemi-nedir
- [S1] Netgsm API dokümanı: https://www.netgsm.com.tr/dokuman/
- [S2] Netgsm toplu SMS fiyatları: https://www.netgsm.com.tr/fiyatlar/toplu-sms
- [S3] İleti Merkezi API genel bakış: https://www.iletimerkezi.com/en/docs/api/overview.md (ayrıca https://www.iletimerkezi.com/llms-developer.txt)
- [S4] İleti Merkezi send-sms: https://www.iletimerkezi.com/en/docs/api/send-sms.md
- [S5] İleti Merkezi toplu SMS fiyatları: https://www.iletimerkezi.com/toplu-sms-fiyatlari
- [S6] Verimor SMS API kullanım kılavuzu: https://github.com/verimor/SMS-API/blob/master/user_guide.md
- [S7] Verimor toplu SMS fiyatları: https://www.verimor.com.tr/toplu-sms

**e-Belge**
- [E1] 509 Sıra No.lu VUK Genel Tebliği, güncel (GİB, 2026): https://cdn.gib.gov.tr/api/gibportal-file/file/getFile?objectKey=MEVZUAT_TEBLIGLER%2FUNIVERSAL%2F2026%2FMEVZUAT_TEBLIGLER_2026_VukTeb509_Guncel.pdf
- [E2] GİB özel entegratör listesi (sayfa erişilebilir, içerik okunmadı): https://ebelge.gib.gov.tr/efaturaozelentegratorlerlistesi.html

**Sağlık**
- [H1] Sağlık Bakanlığı Kayıt ve Tescil, USS entegrasyonu duyurusu (2017): https://kayittescil.saglik.gov.tr/TR-21385/05052017-uss-entegrasyonu-tum-hbys-ureticilerine.html
- [H2] Türk Dermatoloji Derneği, MBYS bilgi notu (7538 sayılı Kanun): https://turkdermatoloji.org.tr/public/media/pdf/MBYS%2015%20Ocak%20Kanun%20De%C4%9Fi%C5%9F.Bilgi%20Notu.pdf
- [H3] Uludağ Bilişim, muayenehanelere e-Nabız veri gönderimi (03.04.2024): https://www.uludagbilisim.com/haber/ozel-poliklinik-ve-muayenehanelere-e-nabiz-veri-gonderme-zorunlulugu-mu-geldi-483
- [H4] Kişisel Sağlık Verileri Yönetmeliği değişikliği, 04.07.2026 (içerik okunmadı): https://www.alomaliye.com/2026/07/04/kisisel-saglik-verileri-hakkinda-yonetmelikte-degisiklik-04-07-2026/
