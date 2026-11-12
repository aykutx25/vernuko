# Çoklu Bilgisayar (Faz 7) — Ana bilgisayar + bağlanan bilgisayarlar

**Karar (sahip, 2026-11-12):** Bir bilgisayar *ana bilgisayar* olur. Veritabanı (SQLite), yedekleme, otomatik mesajlar ve tüm iş kuralları yalnızca onda çalışır. Aynı yerel ağdaki diğer bilgisayarlar ana bilgisayara bağlanır ve aynı ekranları kullanır. Müşteriye ek program (PostgreSQL vb.) kurulmaz. SQLite dosyası **asla** ağ klasöründen paylaşılmaz (dosya kilitleri ağda güvenilir değildir, veri bozulur).

## Mimari
```
Bağlanan PC (istemci)                              Ana bilgisayar
┌──────────────────────────┐   HTTPS (yerel ağ,   ┌──────────────────────────────────┐
│ WPF ekranları + VM'ler   │   sabitlenmiş sertif.)│ Vernuko (WPF) + gömülü Kestrel    │
│ IXxxService → RpcProxy ──┼──────────────────────►│ RpcHost → oturuma özel servisler │
│ yerel UserSession (kopya)│  cihaz anahtarı +     │ (her uzak oturum: ayrı DI kabı,  │
└──────────────────────────┘  oturum belirteci     │  aynı SQLite, kendi UserSession) │
                                                   └──────────────────────────────────┘
```
- Ekranlar servislere **arayüzler** (`ICustomerService` …) üzerinden bağlanır. Ana bilgisayarda arayüz gerçek servise, istemcide `RpcProxy`'ye (DispatchProxy) çözülür. Ekran kodu iki modda aynıdır.
- Çağrı biçimi: `POST /rpc/{Arayüz}/{Metot}`, gövde = parametrelerin JSON'u, yanıt = `Result`/`Result<T>`/değer JSON'u. Yalnızca `Task` döndüren arayüz metotları uzaktan çağrılır. Sunucu tarafında yalnızca arayüzde tanımlı metotlar çağrılabilir (beyaz liste); yansıma ile başka bir şey çağrılamaz.
- Yetki ve lisans kontrolleri **ana bilgisayardaki** servislerde yapılır (istemcideki oturum kopyası yalnızca ekranda düğme gizlemek içindir; güvenlik sınırı değildir).
- Her uzak oturum ana bilgisayarda kendi DI kabını alır: kendi `UserSession`'ı, ortak veritabanı fabrikası/saat/gizli anahtar koruyucusu. Aynı anda ana bilgisayarın kendi ekranı ve istemciler çalışır; yazmalar SQLite IMMEDIATE işlemleriyle sıralanır (çift randevu imkânsızlığı korunur).

## Eşleştirme ve güvenlik
1. Ana bilgisayar: Ayarlar › Çoklu bilgisayar › "Bu bilgisayar ana bilgisayar olsun". Kestrel yerel ağda dinler (varsayılan port 47250), kendi kendine imzalı sertifika üretilir ve DPAPI ile saklanır. Windows Güvenlik Duvarı ilk seferde izin sorar ("Özel ağlar" yeterli; güvenlik duvarı kapatılmaz).
2. Ana bilgisayar ekranında **eşleştirme kodu** (10 dakika geçerli, 12 karakter) ve adres gösterilir.
3. İstemci: kurulumda "Ana bilgisayara bağlan" → adres + kod. İstemci sertifika parmak izini alır, `HMAC(kod, parmak izi)` gönderir; ana bilgisayar kendi parmak iziyle doğrular (araya giren biri farklı sertifika gösterirse eşleşmez) ve karşılığında kendi kanıtını yollar. Başarılıysa istemci parmak izini **sabitler** ve bir **cihaz anahtarı** alır (DPAPI ile saklanır).
4. Sonraki her istek: sabitlenmiş TLS + cihaz anahtarı; giriş yapılınca kullanıcı adı/parola ana bilgisayarda doğrulanır ve bir oturum belirteci verilir.
5. Ana bilgisayar eşleşmiş cihazları listeler ve tek tıkla kaldırabilir. Lisans: eşleşmiş istemci sayısı ≤ lisanstaki cihaz sayısı − 1 (deneme sürümünde 2 istemci).

## İstemcide farklı olanlar
- Yedekleme, geri yükleme, kurulum, lisans etkinleştirme, otomatik mesaj gönderimi yalnızca ana bilgisayarda. İstemci bu ekranlarda ana bilgisayarın durumunu gösterir.
- Dosya seçme/kaydetme istemcide yapılır; içerik bayt olarak gönderilir (servisler zaten bayt alır).
- Ana bilgisayar kapalıysa istemci "Ana bilgisayara ulaşılamıyor" der; çevrimdışı yazma yoktur (çakışan randevu riskini önlemek için bilinçli karar).
- Takvim ve listeler gezinmede ve periyodik olarak yenilenir (diğer bilgisayarların değişiklikleri görünür).

## Uygulama adımları
1. Servis arayüzleri + ekranların arayüzlere geçişi (davranış değişmez).
2. RPC biçimi: JSON (Result/Error dönüştürücüleri), sunucu dağıtıcısı, istemci vekili; yerel döngü testleri.
3. Uzak oturumlar (giriş, kilit, oturum belirteci, oturuma özel DI kabı).
4. TLS + eşleştirme + cihaz anahtarı + cihaz listesi.
5. Masaüstü: ana bilgisayar modu (gömülü Kestrel), istemci modu (başlangıç, kurulum sihirbazı seçeneği), istemciye özel ekran kısıtları, yenileme.
6. Uçtan uca: başarı senaryosunun istemciden ana bilgisayara karşı çalıştırılması; belgeler.

## Durum (0.9.0)
| Adım | Durum |
|---|---|
| 1. Servis arayüzleri | ✅ `ServiceContracts.cs` derlenmiş servislerden üretilir (`tools/ServiceContractGenerator`); `ServiceContractTests` eksik metodu yakalar |
| 2. Çağrı biçimi | ✅ `Vernuko.Remote`: JSON (Result/Error, IReadOnlySet dönüştürücüleri), beyaz listeli dağıtıcı, DispatchProxy vekil |
| 3. Uzak oturumlar | ✅ oturum başına ayrı servis kabı; giriş, kilit, çıkış, 12 saat boşta kalınca sona erme; girişten önce yalnızca işletme adı okunabilir |
| 4. TLS + eşleştirme | ✅ kendinden imzalı sertifika (DPAPI ile saklı), parmak izi sabitleme, tek kullanımlık kod + karşılıklı HMAC, cihaz anahtarı (yalnızca özeti saklanır), lisansa göre cihaz sınırı, istek sınırları |
| 5. Masaüstü | ✅ Ayarlar › Çoklu bilgisayar; kurulum sihirbazında "Ana bilgisayara bağlan"; istemci modunda yerel veritabanı yok; yedekleme/mesaj/lisans ekranları ana bilgisayara yönlendirir |
| 6. Uçtan uca | ✅ test: ana bilgisayar modu açılır → sihirbazdan eşleşme → yeniden başlama → giriş → tüm ekranlar → başarı senaryosu bağlı bilgisayarda → cihaz kaldırılınca giriş ekranı |

**Henüz denenmedi:** iki gerçek Windows bilgisayarda yerel ağ üzerinden (Güvenlik Duvarı izni, bilgisayar adıyla bağlanma, Wi-Fi kopmaları). İlk gerçek kurulumda `kontrol.cmd` sonrası elle denenmeli.

**Bilinen sınırlar:** ana bilgisayar kapalıyken bağlı bilgisayarlar çalışmaz (bilinçli); takvim ekranı birden çok bilgisayar varken dakikada bir kendini yeniler (anlık bildirim yok; randevu düzenlenirken yenilenmez); bağlı bilgisayarda müşteri fotoğrafı/ek dosya 32 MB ile sınırlı.
