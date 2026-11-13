# Lisans Sunucusu (Faz 6)

> **Karar (sahip, 2026-11-13): lisans sunucusu olarak WordPress eklentisi kullanılacak.** Satış sitesi WordPress + WooCommerce olacağı için ayrı sunucu (VPS) gerekmez. Ayrıntı: [`wordpress/vernuko-license/README.md`](../wordpress/vernuko-license/README.md). Eklenti, aşağıdaki .NET sunucusuyla **aynı protokolü ve aynı imzalı belge biçimini** kullanır; masaüstü uygulama ikisiyle de çalışır (testler her ikisini de doğrular). Bu belge .NET sunucusunu anlatır; VPS ile çalışmak gerekirse diye yedek seçenek olarak depoda kalır.

`src/Vernuko.LicenseServer` — tek süreçli ASP.NET Core (.NET 10) + SQLite. Küçük bir Linux VPS'te, TLS sağlayan bir ters vekilin (ör. Caddy) arkasında çalışır. **Masaüstü uygulamayla birlikte dağıtılmaz.**

**Bu sunucuya müşterinin randevu, müşteri veya ödeme kayıtları gelmez.** Gelenler yalnızca şunlardır: lisans anahtarı, özetlenmiş cihaz kimliği (SHA-256'nın ilk 32 karakteri) ve etkinleştirme kimliği. Paddle tarafından da abonelik kimliği, durum ve dönem tarihleri gelir.

## Ne yapar
| Uç nokta | Görev |
|---|---|
| `POST /v1/activate` | Lisans anahtarı + cihaz → imzalı lisans belgesi. Cihaz sınırı dolmuşsa 409 döner ve "eski bilgisayardan kaldırın" der. Aynı bilgisayar ikinci kez etkinleştirirse yeni koltuk harcanmaz. |
| `POST /v1/refresh` | Uygulama yaklaşık haftada bir yeni belge alır. Abonelik bittiyse 403 döner; uygulama elindeki belge bitene kadar çalışır, sonra salt-okunur olur. |
| `POST /v1/deactivate` | "Bu bilgisayardan kaldır" (yeni bilgisayara taşıma). Bilgisayardaki veriler silinmez. |
| `POST /v1/trial` | Cihazın ilk deneme başlangıcını kaydeder. Program yeniden kurulsa da 30 gün baştan başlamaz. |
| `POST /v1/updates/permission`, `GET /v1/updates/feed/{dosya}` | Velopack güncelleme dosyaları yalnızca etkinleştirilmiş ve aboneliği süren kurulumlara verilir. Uygulama bu adrese istek atarken `X-Vernuko-Activation` ve `X-Vernuko-Device` başlıklarını kendisi ekler. |
| `POST /v1/webhooks/paddle` | Paddle Billing bildirimleri: imza doğrulama, olay kimliğiyle tekrar koruması, `occurred_at` ile sıra dışı gelen eski olayları yok sayma. |
| `/v1/admin/…` | Elle lisans oluşturma (havale/EFT satışı), listeleme, ayrıntı, anahtarı yeniden gösterme, süre uzatma, iptal, destek için cihaz kaldırma. `X-Admin-Key` başlığı ister. |
| `GET /health` | Uptime izleme için. |

Herkese açık uç noktalar her IP için dakikada 30 istekle sınırlıdır; aşılınca 429 döner.

## Kurallar (masaüstü değerlendiricisiyle aynı)
- Belgedeki geçerlilik (`exp`): abonelik sürerken **ödenen dönem sonu + 7 gün ödeme toleransı**. Abonelik iptal edilmiş ya da duraklatılmışsa ödenen dönem sonu; tolerans eklenmez, süre erken de kesilmez.
- Yenileme zamanı (`rfr`): şimdi + 7 gün (en geç `exp`). Bu tarihten sonra 14 gün çevrimdışı çalışılabilir.
- İptal yalnızca yenilemeyi durdurur, ödenmiş dönemi kısaltmaz.
- Lisans yalnızca doğrulanmış webhook'tan ya da yöneticinin elle oluşturmasından doğar. Tarayıcı yönlendirmesine bakılarak lisans verilmez.
- Lisans anahtarı biçimi: `VRNK-XXXXX-XXXXX-XXXXX-XXXXX` (100 bit). Sunucu anahtarın kendisini saklamaz; arama için SHA-256 özetini tutar. Anahtar, lisans kimliği ve sunucu sırrından yeniden üretilebilir; kaybolan anahtar bu sayede yeniden gönderilebilir.

## Kurulum (yeni sunucu)
1. Anahtarları üretin (bir kez, sunucuda):
   ```bash
   ./Vernuko.LicenseServer generate-key /etc/vernuko-license
   ```
   Komutun yazdıkları:
   - imzalama anahtarı ve anahtar sırrı dosyaları (`chmod 600`),
   - **açık anahtar** (masaüstü derlemesine girer),
   - **yönetici anahtarı** (yalnızca bir kez gösterilir; parola yöneticisinde saklayın),
   - yönetici anahtarının SHA-256 özeti (sunucu ayarına girer).
2. Ayarları ortam değişkenleriyle verin (systemd `Environment=`/`EnvironmentFile=`). Sırları depoya ya da `appsettings.json`'a yazmayın:
   ```
   LicenseServer__ConnectionString=Data Source=/var/lib/vernuko-license/license.db
   LicenseServer__SigningKeyPath=/etc/vernuko-license/signing-key.pem
   LicenseServer__SigningKeyId=2026-11
   LicenseServer__KeySecretPath=/etc/vernuko-license/key-secret.txt
   LicenseServer__AdminKeySha256=<özet>
   LicenseServer__PaddleWebhookSecretPath=/etc/vernuko-license/paddle-secret.txt
   LicenseServer__PaddlePrices__pri_XXXX__Edition=Standart
   LicenseServer__PaddlePrices__pri_XXXX__MaxDevices=1
   LicenseServer__ReleasesPath=/var/lib/vernuko-license/releases
   ```
3. Sunucu `127.0.0.1:5080` adresini dinler. TLS ters vekil, örnek Caddyfile:
   ```
   lisans.ornek.com {
       reverse_proxy 127.0.0.1:5080
   }
   ```
4. Masaüstü sürüm derlemesi için GitHub depo **değişkenlerini** (secret değil, ikisi de açık bilgi) tanımlayın:
   - `VERNUKO_LICENSE_SERVER_URL=https://lisans.ornek.com/`
   - `VERNUKO_LICENSE_KEYS=2026-11:<açık anahtar base64>`
   - Otomatik güncelleme de sunucudan verilecekse: `VERNUKO_UPDATE_URL=https://lisans.ornek.com/v1/updates/feed`. Bu durumda `vpk pack` çıktısını `ReleasesPath` klasörüne kopyalayın.
5. Paddle panelinde bir bildirim hedefi (notification destination) ekleyin: adres `https://lisans.ornek.com/v1/webhooks/paddle`, abonelik olayları seçili. Verilen `pdl_ntfset_…` sırrını `paddle-secret.txt` dosyasına yazın.

Sunucu açılışta veritabanı geçişlerini kendisi uygular. Sırlar eksik ya da geçersizse hemen durur.

## İşletme
- **Yedek:** `license.db` dosyasını günde bir kez şifreleyip başka bir bölgeye kopyalayın (ör. `sqlite3 license.db ".backup …"` ve ardından `age`/`gpg`). Anahtar dosyaları ayrı ve çevrimdışı bir yerde de saklanmalı. İmzalama anahtarı kaybolursa yeni anahtar kimliğiyle yeni sürüm çıkarmak gerekir.
- **Sunucu kapalıyken:** müşteriler etkilenmez. Elindeki belgeyle `rfr` + 14 gün çalışırlar. Hedeflenen kurtarma süresi 4 saat: yeni VPS, son yedek, aynı anahtarlar.
- **Anahtar döndürme:** yeni bir anahtar kimliğiyle yeni anahtar üretin. Önce eski ve yeni açık anahtarı birlikte içeren bir masaüstü sürümü yayınlayın (`VERNUKO_LICENSE_KEYS=eski:…|yeni:…`), sonra sunucuyu yeni anahtara geçirin.
- **Destek:** `GET /v1/admin/licenses?q=…` ile arayın. `POST /v1/admin/activations/{id}/deactivate` ile bozulan bilgisayarın koltuğunu boşaltın. `GET /v1/admin/licenses/{id}/key` ile anahtarı yeniden gönderin (denetim kaydına yazılır).

## Henüz yok / bilinen sınırlar
- **Gerçek bir Paddle hesabıyla denenmedi.** İmza biçimi ve olay alanları resmi belgelerden alındı (2026-11-12) ve testlerle doğrulandı. İlk kullanımda Paddle sandbox ile denenmeli.
- **iyzico abonelik webhook'u yok.** Yurt içi TL satışı şimdilik elle lisans oluşturma (`/v1/admin/licenses`) ile yapılır.
- **Lisans anahtarı müşteriye otomatik e-postayla gitmiyor.** Webhook'tan doğan lisansın anahtarı yönetici uç noktasından alınıp elle iletilir.
- Yönetici arayüzü yok; uç noktalar `curl` veya benzeri bir araçla kullanılır.
- Sunucu tek süreç ve tek SQLite dosyasıdır; yüksek erişilebilirlik hedeflenmedi.
