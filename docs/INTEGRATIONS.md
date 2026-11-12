# F. Mesajlaşma ve Entegrasyon Mimarisi

Kaynaklar: [research/messaging-and-backup-apis.md](research/messaging-and-backup-apis.md), [research/turkey-market-and-law.md](research/turkey-market-and-law.md) (İYS, SMS sağlayıcıları).

## İlkeler
1. **Entegrasyonlar kapalıyken çekirdek çalışır.** Hiçbir randevu/müşteri işlemi bir entegrasyona bağlı değildir.
2. **Ücretli her şey varsayılan kapalı, otomatik harcama limiti 0 ₺.** İşletme etkinleştirir; maliyet işletmenin kendi sağlayıcı hesabına yazılır.
3. **Sağlayıcı fiyatları koda gömülmez.** Tahminler güncellenebilir tarifelerden okunur; gerçek maliyet varsa sağlayıcıdan alınır.
4. **Mock entegrasyonlar gerçek gibi gösterilmez.** Faz 1'de yalnızca manuel WhatsApp vardır; ekranlar diğerlerini "Kapalı / henüz mevcut değil" olarak gösterir.
5. **Veri minimizasyonu:** mesajlar yalnızca ad, tarih/saat, işletme adı ve telefon içerir; sağlık/terapi profillerinde hizmet adı şablonda yazsa bile **eklenmez**.

## Durum ve öncelik
| Entegrasyon | Durum | Öncelik |
|---|---|---|
| Manuel WhatsApp (wa.me) | **Uygulandı** (randevu satırı › "WhatsApp ile hatırlat") | P0 |
| Yerel şifreli yedek | **Uygulandı** | P0 |
| CSV dışa aktarım (müşteri listesi), JSON (müşteri bazlı KVKK talebi) | **Uygulandı** | P0 |
| SMS — İleti Merkezi | **Uygulandı (Faz 5):** otomatik randevu hatırlatma, işletmenin kendi hesabı; istek/yanıt biçimi resmi belgeden (2026-11-12) alındı ve testli, **gerçek hesapla henüz denenmedi** | P1 |
| SMS — Netgsm, Verimor | Yok. Netgsm'in API belgesi alınamadı (sayfa boş şablon); tahminle yazılmadı. Verimor gönderen IP'yi beyaz listeye almayı zorunlu tutuyor | P1 |
| WhatsApp Cloud API (işletmenin kendi Meta hesabı) | **Uygulandı (Faz 5):** onaylı utility şablonuyla hatırlatma ({{1}} ad, {{2}} tarih, {{3}} saat, {{4}} işletme); belgeye göre testli, **gerçek hesapla henüz denenmedi**; teslim durumu webhook gerektirdiği için "sağlayıcıya iletildi" olarak tutulur | P1 |
| Bulut yedek (OneDrive / Google Drive / Dropbox) | **Uygulandı (Faz 4):** eşitleme klasörüne şifreli kopya; OAuth API yöntemi yok | P1 |
| SMTP e-posta | Faz 5 | P1 |
| ICS dosyası (randevuyu takvime ekle) | Faz 5 | P1 |
| Excel / PDF raporlar | Faz 5 (ClosedXML MIT; PDF için QuestPDF Community — gelir <1M USD) | P1 |
| Google / Microsoft Calendar senkronu | Faz 7+ | P2 |
| Online randevu portalı, web sitesi entegrasyonu | Sunucu gerektirir — Model C | P2 |
| e-Arşiv / e-Fatura (özel entegratör) | Mevzuat ve entegratör seçimi sonrası | P2 |

## Mesajlaşma (Faz 5 — uygulanan)
- **Giden kutusu** (`OutboxMessages`): her hatırlatma tekil anahtarla (`reminder:<randevu>:<saat>:<kanal>`) yazılır; aynı randevu saati için ikinci mesaj oluşmaz. Randevu iptal/taşınırsa bekleyen hatırlatma iptal edilir, yeni saate yeni hatırlatma planlanır.
- **Planlayıcı ve gönderici** yalnızca Vernuko açıkken 5 dakikada bir çalışır. Varsayılan: hatırlatma kapalı, **aylık limit 0** (hiç mesaj gitmez). Sessiz saatler (varsayılan 21:00–09:00) dışında gönderilir; sabah erken randevularda hatırlatma bir önceki akşama alınır. Lisans salt-okunur moddaysa gönderim durur.
- **Gönderim anında yeniden kontrol:** randevu hâlâ açık ve saati aynı mı, müşteri ilgili kanala izin veriyor mu, aylık limit dolmuş mu.
- **Hatalar:** geçici hatalar (ağ, zaman aşımı, sağlayıcı hız sınırı, 5xx) 1/5/15/60 dk aralıkla en çok 5 kez denenir; kalıcı hatalar (geçersiz numara, yetki, onaysız başlık/şablon) tekrar denenmez ve Türkçe açıklamayla listelenir.
- **Kimlik bilgileri** (İleti Merkezi key/hash, WhatsApp token) Windows hesabına bağlı (DPAPI) şifreli saklanır; ekranda gösterilmez; başka bilgisayarda açılmaz (yeniden girilir).
- **Bilinen sınır:** sağlayıcı mesajı kabul ettikten hemen sonra program çökerse aynı mesaj 10 dakika sonra bir kez daha gönderilebilir (sağlayıcılar tekrar engelleme anahtarı sunmuyor).
- Ayarlar › **Mesajlaşma**: kanal, kaç saat önce, sessiz saatler, aylık limit, SMS birim fiyatınız (tahmin için), hesap bilgileri, deneme mesajı, giden mesajlar listesi ve aylık özet.

## Mesajlaşma mimarisi (Faz 5 tasarımı)
```
Randevu olayı ──▶ Bildirim planlayıcı ──▶ OutboxMessages (SQLite, işlemle aynı transaction)
                    (şablon + kanal +            │
                     izin + sessiz saat)          ▼
                                         Gönderim işçisi (uygulama açıkken)
                                         ├─ bütçe/limit kontrolü (aylık adet + ₺)
                                         ├─ mükerrer engeli (IdempotencyKey = olay+kanal+alıcı)
                                         ├─ ISmsProvider / IWhatsAppProvider
                                         └─ sonuç → MessageLog (durum, maliyet, hata)
```
- **Outbox deseni:** mesaj kaydı randevu işlemiyle aynı veritabanı işleminde yazılır; uygulama çökse bile kaybolmaz, iki kez gönderilmez.
- **Şablonlar:** `{Ad}`, `{Tarih}`, `{Saat}`, `{Hizmet}`, `{Personel}`, `{Isletme}`, `{Telefon}` (uygulandı: `MessageTemplates`). Bilinmeyen yer tutucu görünür bırakılır (yazım hatası fark edilsin).
- **Amaç ayrımı:** operasyonel (oluşturma, hatırlatma, erteleme, iptal) ile **ticari** (kampanya, doğum günü) ayrı kuyruk ve izin kuralları. Ticari gönderim yalnızca `MarketingConsent` + İYS kontrolüyle.
- **Gönderim limiti ve bütçe:** aylık adet ve ₺ tavanı; aşımda gönderim durur ve açık onay istenir.
- **Hata yönetimi:** geçici hatalarda üstel geri çekilme (en çok 5 deneme), kalıcı hatalarda (geçersiz numara, izin yok) yeniden deneme yok; başarısızlar "Entegrasyonlar ve Maliyetler" ekranında listelenir.
- **Sessiz saatler:** varsayılan 21:00–09:00 arası otomatik mesaj yok.
- **Telefon normalizasyonu:** E.164 (uygulandı: `PhoneNumber`).

### SMS
- Tek arayüz `ISmsProvider` (uygulandı), sağlayıcı adaptörleri ayrı sınıflar.
- Netgsm, İleti Merkezi, Verimor: işletmenin kendi hesabı ve onaylı başlığı. **Verimor gönderen IP'yi beyaz listeye almayı istiyor** — dinamik IP'li masaüstü için sorun; Netgsm'de IP kısıtı isteğe bağlı. Sağlayıcıların teslim raporu (DLR) çoğunlukla sorgu ile alınabildiği için masaüstünde periyodik sorgu kullanılır.
- **Segment hesabı (gösterilecek):** GSM-7 160/153 · Türkçe single-shift 155/149 · UCS-2 70/67. ğ, ı, İ, ş, Ş, ç içeren metin single-shift desteklenmiyorsa UCS-2'ye düşer (2 segment olabilir). Sağlayıcının hangi modu desteklediği adaptör profilinde tanımlanır; editör canlı karakter/segment ve tahmini maliyet gösterir.
- Mükerrer engeli: aynı randevu + amaç + alıcı için tek mesaj.

### WhatsApp
- **Manuel (uygulandı):** `https://wa.me/<numara>?text=<mesaj>` açılır, sekreter kontrol edip kendisi gönderir. Ücretsiz; otomatik mesajlaşma olarak **tanıtılmaz**.
- **Cloud API (Faz 5):** işletmenin kendi Meta işletme hesabı ve System User token'ı (DPAPI ile korunur). Onaylı **utility** şablonları; pazarlama şablonları ayrı ve izinli.
  - 1 Temmuz 2025'ten beri mesaj başı fiyatlandırma; Türkiye için utility/authentication/service ≈ $0,0009, marketing ≈ $0,0109 (Kasım 2026, resmi tablo). 1 Ekim 2026'dan beri pencere içi utility ve servis mesajları da ücretli; numara başı ayda 1.000 ücretsiz servis mesajı.
  - **Teslim/okundu durumu yalnızca webhook ile gelir** (herkese açık HTTPS uç nokta gerekir; geçmişi sorgulayan API yok). Sunucusuz masaüstü için seçenekler: (a) yalnızca gönder, durum "kabul edildi" (varsayılan), (b) işletmenin kendi küçük aktarıcısı, (c) BSP. Vernuko'nun kendi sunucusunda müşteri mesaj trafiği tutulmaz.
  - Yeni hesaplar 250 alıcı/gün limitiyle başlar. Sağlık bilgisi içeren mesajlar politika gereği sınırlıdır → hizmet adı gizleme varsayılanı.
- **Resmî olmayan WhatsApp Web otomasyonu, tarayıcı taklidi veya toplu spam asla kullanılmaz.**

### E-posta
SMTP (işletmenin kendi sunucusu/hesabı), TLS zorunlu, parola DPAPI ile korunur. ICS ekli randevu bildirimi.

## Maliyet Kontrol Merkezi
Ayarlar › **Entegrasyonlar ve Maliyetler** ekranı (Faz 1'de bilgilendirici sürüm uygulandı): aktif entegrasyonlar, durum, maliyet, not. Faz 5'te eklenecek: SMS/WhatsApp adetleri, tahmini ve gerçek maliyet, aylık limitler, son başarısız işlemler, bulut kota ve yedek boyutu.

## Hukuki not (avukat onayı gerekli)
- Randevu hatırlatmaları İYS onayı gerektirmeyen "hizmetle ilgili bildirim" kapsamında değerlendirilebilir, ancak içerikte **tanıtım/özendirme olamaz** (Yön. md. 6). Yönetmelik "randevu" kelimesini açıkça kullanmıyor.
- Doğum günü/kutlama mesajları **ticari ileti** sayılır (md. 5/1) → onay + İYS.
- WhatsApp mesajlarının 6563 kapsamındaki durumu, "kalan seans / yenileme" hatırlatmalarının sınıflandırılması avukata sorulacak (bkz. turkey-market-and-law.md › C).
