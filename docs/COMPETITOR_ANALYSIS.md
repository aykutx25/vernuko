# A. Rakip Araştırması ve Özellik Matrisi

Bu belge, `docs/research/` altındaki kaynaklı raporların özetidir. Tüm rakamlar ve iddialar kaynak bağlantılarıyla birlikte o raporlarda yer alır; burada yalnızca karar için gereken sentez vardır.

| Rapor | Kapsam |
|---|---|
| [research/intl-competitors-1.md](research/intl-competitors-1.md) | Fresha, Booksy, Pabau, SimplyBook.me, Setmore, Vagaro |
| [research/intl-competitors-2.md](research/intl-competitors-2.md) | Cliniko, Mindbody, Zen Planner, Clio Grow, ezyVet, Tekmetric |
| [research/turkey-market-and-law.md](research/turkey-market-and-law.md) | SalonJet, Salun, Rezzim, Arvengo, Salon Randevu, Orest, CosmeticSoft, Hursoft, Diş Hekimi Pro, DentalBulut, GymKod (+ KVKK, İYS, e-Arşiv, sağlık) |
| [research/licensing-payments-signing.md](research/licensing-payments-signing.md) | Lisanslama, ödeme altyapısı, kod imzalama, kurulum/güncelleme |
| [research/messaging-and-backup-apis.md](research/messaging-and-backup-apis.md) | WhatsApp, Google Drive, OneDrive, Dropbox, şifreli yedek, SMS segment hesabı |

**Yöntem ve sınırlar.** Araştırma 11–12 Kasım 2026'da yapıldı. Capterra, G2, GetApp ve Şikayetvar gibi bazı yorum siteleri otomatik erişime 403 döndürdü; bu nedenle yorum analizi Trustpilot, App Store, Google Play ve forum derlemelerine dayanıyor ve ilgili ifadeler raporlarda "ikincil" olarak işaretli. Doğrulanamayan özellikler kesin bilgi gibi yazılmadı.

---

## 1. Pazarın genel resmi

1. **Offline çalışan Windows uygulaması neredeyse yok.** İncelenen 12 uluslararası üründe resmi Windows masaüstü uygulaması yok; belgelenmiş offline mod yalnızca Pabau GO (iOS, kısıtlı) ve ezyVet Go'da (mobil, kısıtlı) var. Kesinti şikâyetleri (Pabau, Vagaro) offline-first yaklaşımın gerçek bir ihtiyaç olduğunu gösteriyor.
2. **Türkiye'de ürünlerin çoğu bulut (web + mobil), 700–4.000 ₺/ay.** Masaüstü rakipler (Orest 17.999 ₺, CosmeticSoft 21.000 ₺ süresiz) eski tip tek seferlik lisanslar. "Offline + müşteriye ait yedek + kendi SMS hesabı" modelini açıkça sunan tek örnek diş hekimliğinde ücretsiz Diş Hekimi Pro.
3. **Güven sorunları ticari davranıştan geliyor:** pazar yeri komisyonu (Fresha %20, Booksy %30), habersiz zam, iptal sonrası tahsilat, fiyatı gösterilmeyen eklentiler, ücretli veri dışa aktarımı (Pabau tam export £450+KDV, Fresha Data Connector $295/ay/lokasyon), "lisans bitince sistem kilitlendi" (Türk masaüstü ürünleri).
4. **Mesaj maliyeti farklı modellerle satılıyor:** dahil (Booksy, Vagaro), kota (Fresha, Setmore), kredi (SimplyBook, Pabau, Cliniko 10¢/mesaj). Türkiye'de aracı firmaların SMS kredisi doğrudan sağlayıcıdan yaklaşık 2 kat pahalı (500 SMS: 399,99 ₺ vs Netgsm 198 ₺ / İleti Merkezi 189 ₺). "Kendi SMS hesabını getir" modeli Türk rakiplerde yaygın (Arvengo, SalonJet, Diş Hekimi Pro).
5. **Temel özellikler sık sık üst pakete kilitleniyor:** oda/kaynak yönetimi (Mindbody Accelerate), roller ve komisyon (Fresha Team), SMS ve depozito (Setmore Pro), iade ve kayıtlı kart (SimplyBook Premium).
6. **Sektöre özgü eksikler:** Cliniko'da paket yok (kredi ile geçici çözüm), oda/cihaz takvimi yok; Tekmetric'te tekrarlayan randevu üçüncü parti eklentide, araç silinince faturalar da silinmiş (şikâyet); Clio Grow'da intake ile dava yönetimi ayrı ürün.

---

## 2. Özellik matrisi (özet)

Gösterim: ● var · ◐ kısıtlı / üst pakette / ek ücretli · ○ yok · ? doğrulanamadı. Ayrıntı ve kaynaklar için ilgili rapora bakın.

| Özellik | Fresha | Booksy | Pabau | SimplyBook | Setmore | Vagaro | Cliniko | Mindbody | Türk bulut (Salun/Rezzim/SalonJet) | **Vernuko Faz 1** |
|---|---|---|---|---|---|---|---|---|---|---|
| Offline / masaüstü | ○ | ○ | ◐ iOS | ○ | ○ | ○ | ○ | ○ | ○ | **● Windows, offline** |
| Personel takvimi | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● |
| Oda/cihaz takvimi ve çakışma | ● | ? | ● | ◐ | ○ | ◐ | ○ | ◐ | ? | **● (DB seviyesinde de)** |
| Seans paketi (kalan hak) | ● | ? | ● | ◐ | ○ | ● | ○ | ● | ● (çoğunda) | **● defter tabanlı** |
| Hazırlık/temizlik aralığı | ? | ? | ? | ? | ? | ? | ? | ? | ? | ● |
| Tekrarlayan randevu | ? | ? | ? | ◐ | ? | ? | ● | ● | ? | ○ (Faz 3, P1) |
| Bekleme listesi | ● | ● | ? | ? | ? | ● | ● | ● | ? | ○ (P1) |
| Online randevu | ● | ● | ● | ● | ● | ● | ● | ● | ● | ○ (P2, isteğe bağlı) |
| Hatırlatma | ● | ● | ● (kredi) | ● (kredi) | ◐ Pro | ● | ● (kredi) | ● | ● | ◐ manuel WhatsApp; otomatik Faz 5 |
| Rol / işlem bazlı yetki | ◐ Team | ? | ● | ◐ | ◐ | ● | ● | ● | ? | ● 28 izin, 7 rol |
| Gizli notlar (yazar bazlı) | ? | ? | ● | ? | ? | ? | ● | ● | ? | ◐ izin hazır, modül Faz 3 |
| Kasa / tahsilat | ● | ● | ● | ◐ | ◐ | ● | ● | ● | ● | ● (fatura değil) |
| Komisyon/prim | ◐ Team | ? | ● | ? | ? | ● | ? | ◐ | ● | ○ (Faz 2, P1) |
| Tam veri dışa aktarımı | ◐ ücretli | ? | ◐ £450 | ? | ? | ◐ destekten | ● | ◐ | ? | ● CSV + müşteri bazlı JSON, yerel DB |
| Lisans bitince veri erişimi | n/a | n/a | ◐ kilit şikâyeti | n/a | n/a | n/a | n/a | n/a | ◐ kilit şikâyeti | **● salt-okunur, silme yok** |
| Müşteriye ait şifreli yedek | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | **● AES-256-GCM, kurtarma anahtarı** |

---

## 3. Kategorilere ayrılmış özellik kararı

### 3.1 Vernuko'da mutlaka bulunmalı (P0 — Faz 1–2)
- Offline çalışan tek bilgisayar modu (yerel SQLite) — **uygulandı**
- Personel + oda + cihaz birlikte rezervasyonu, hazırlık/temizlik aralığı, çakışma kontrolü — **uygulandı** (uygulama + veritabanı tetikleyicisi)
- Seans paketi: kalan hak, iptalde hak düşmemesi, tamamlamada düşme, düzeltme kaydı, dondurma/uzatma — **uygulandı**
- Müşteri kartı, Türkçe uyumlu hızlı arama, mükerrer kayıt uyarısı (birleştirme yok) — **uygulandı**
- Tahsilat kaydı, kapora, kalan bakiye, ters kayıtla düzeltme — **uygulandı**
- İşlem bazlı yetki, denetim günlüğü, otomatik kilit — **uygulandı**
- Şifreli yerel yedek + geri yükleme + doğrulama — **uygulandı**
- Lisans bittiğinde verinin silinmemesi, salt-okunur mod, dışa aktarım — **uygulandı (lisans sunucusu Faz 6)**

### 3.2 Rakiplerden daha iyi uygulanabilir
- **Veri sahipliği:** tam dışa aktarım ücretsiz ve tek tık (rakiplerde ücretli/kısıtlı).
- **Maliyet şeffaflığı:** "Entegrasyonlar ve Maliyetler" ekranı, varsayılan harcama limiti 0 ₺, SMS segment/maliyet tahmini (Faz 5).
- **Fiyat öngörülebilirliği:** çekirdek operasyon (kaynak, paket, yetki, export) pakete kilitlenmez; farklılaşma şube sayısı ve iletişim hacmiyle (bkz. COST_MODEL.md).
- **Kaynak takvimi temel pakette** (Mindbody'de üst pakette, Cliniko'da yok).
- **Seans düşümünün geri alınabilir ve denetlenebilir olması** (append-only defter; Pabau/Fresha referans).
- **Finansal kayıtların asla kademeli silinmemesi** (Tekmetric şikâyeti) — veritabanı tetikleyicisiyle zorunlu.
- **Türkiye'ye uygunluk:** TL, Türkçe arama (ı/İ/ş/ğ), T.C. kimlik doğrulaması (isteğe bağlı), İYS ayrımı (operasyonel bildirim vs ticari ileti).

### 3.3 İsteğe bağlı modül olmalı
Otomatik SMS ve WhatsApp API, bulut yedek (Drive/OneDrive/Dropbox), online randevu portalı, Google/Microsoft takvim senkronu, stok, komisyon/prim, hediye kartı, grup dersi/kontenjan, evcil hayvan, araç, gizli uzman notları, belge kontrol listeleri.

### 3.4 İlk sürüm için gereksiz
Pazar yeri (müşteri bulma platformu), AI resepsiyonist, markalı mobil uygulama, pazarlama otomasyonu, sadakat programı, kiosk check-in, kuşak/derece takibi.

### 3.5 Güvenlik, hukuki veya maliyet riski taşıyor
| Özellik | Risk | Karar |
|---|---|---|
| Doğum günü / kampanya SMS'i | Ticari ileti: onay + İYS kaydı gerekir (Yön. md. 5/1) | Pazarlama izni müşteri kartında ayrı tutuluyor; toplu ticari gönderim Faz 5'te İYS kontrolüyle |
| Bulut yedeğe otomatik gönderim | KVKK'da yurt dışı aktarım sayılabilir; standart sözleşme + 5 iş günü bildirim | Faz 4; şifreleme zorunlu, kurulumda açık uyarı, **avukat görüşü alınmadan varsayılan kapalı** |
| Tıbbi kayıt, reçete, teşhis | Sağlık Bakanlığı tescil/MBYS-e-Nabız yükümlülükleri, özel nitelikli veri | Kapsam dışı; klinik profilleri yalnızca ön büro |
| Resmî fatura / e-Arşiv | 1/1/2026'dan itibaren kâğıt fatura bitti; yanlış belge vergi riski | Vernuko yalnızca tahsilat kaydı tutar; entegratör entegrasyonu P2 |
| Resmî olmayan WhatsApp Web otomasyonu | Hesap kapatma, politika ihlali | Asla; yalnızca wa.me (manuel) ve resmî Cloud API |
| Pazar yeri komisyonu | Güven kaybı (Fresha/Booksy şikâyetleri) | Yok |
| Kayıtlı kart / ön provizyon | PCI-DSS, yerel sağlayıcı bağımlılığı | Kapsam dışı; kapora manuel kaydedilir |

---

## 4. Fikrî mülkiyet
Vernuko'nun arayüzü, ikonları (kendi 24×24 çizgi ikon seti), renk tokenları, veri modeli ve kodu bu proje için sıfırdan üretilmiştir. Rakiplerden yalnızca genel işlevler ve sektör standardı iş akışları (ör. "paket kalan hakkı", "kaynak takvimi") referans alınmıştır; hiçbir rakip kodu, görseli, metni veya marka unsuru kullanılmamıştır.
