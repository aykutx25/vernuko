# Maliyet Modeli

Fiyatlar Kasım 2026'da resmi sayfalardan okunmuştur (kaynaklar: [research/licensing-payments-signing.md](research/licensing-payments-signing.md), [research/messaging-and-backup-apis.md](research/messaging-and-backup-apis.md), [research/turkey-market-and-law.md](research/turkey-market-and-law.md)). "Tahmin" yazanlar doğrulanmamış varsayımlardır. Sağlayıcı ücretleri uygulama koduna gömülmez.

## 1. İşletmenin (müşterinin) maliyetleri
| Kalem | Model A varsayılanı | Kim öder | Not |
|---|---|---|---|
| Sunucu | **Yok** | — | Veri işletmenin bilgisayarında |
| Vernuko aboneliği | Lisans fiyatı (karar verilmedi) | İşletme | Çekirdek operasyon pakete kilitlenmez |
| Yerel/USB yedek | 0 ₺ | — | Kendi diski |
| Bulut yedek (Faz 4) | Kendi Drive/OneDrive/Dropbox kotası | İşletme | Yedek boyutu tahmini ve kota uyarısı gösterilir |
| SMS (Faz 5) | Kapalı | İşletme → kendi sağlayıcısı | Örnek: 500 SMS ≈ 189–198 ₺ (İleti Merkezi / Netgsm, KDV+ÖİV dahil). Aracı uygulamalarda aynı adet ≈ 400 ₺. |
| WhatsApp Cloud API (Faz 5) | Kapalı | İşletme → kendi Meta hesabı (USD/EUR) | Türkiye: utility ≈ $0,0009/mesaj, marketing ≈ $0,0109; numara başı ayda 1.000 ücretsiz servis mesajı |
| Manuel WhatsApp | 0 ₺ | — | Sekreter gönderir |

**Varsayılan otomatik harcama limiti 0 ₺.** Ücretli entegrasyon etkinleştirilmeden hiçbir ücretli işlem yapılmaz; limit aşımında gönderim durur veya açık onay istenir.

## 2. Vernuko'nun (satıcının) maliyetleri
| Kalem | Seçim | Maliyet | Not |
|---|---|---|---|
| Lisans servisi | Kendi API'miz, tek küçük VPS | Tahmin: küçük bir VPS aylık birkaç–birkaç on € (sağlayıcıya göre; doğrulanmadı) + alan adı + TLS (Let's Encrypt ücretsiz) | Müşteri trafiği yok; yalnızca aktivasyon/yenileme |
| Alternatif lisans | Keygen Cloud | 100 aktif kullanıcıya kadar 0 $; 1.000'e kadar $99/ay veya $990/yıl | Hızlı başlangıç için |
| Ödeme (uluslararası) | Paddle (MoR) | İşlem başı %5 + $0,50; sabit ücret yok | KDV/VAT yönetimi dahil |
| Ödeme (yurt içi TL) | iyzico Subscription | İlk 3 ay ücretsiz, sonra 199 ₺/ay + işlem komisyonu (komisyon oranı sözleşmeye bağlı, doğrulanmadı) | |
| Stripe (yalnızca yurt dışı şirketle) | Stripe Atlas | $500 kurulum + $100/yıl + yurt dışı muhasebe/vergi yükümlülükleri | MoR değil; Form 5472 |
| Kod imzalama | OV + bulut HSM | Certum SimplySign ~€209'dan; SSL.com OV $129/yıl + eSigner $20/ay | Artifact Signing ($9,99/ay) Türkiye'ye kapalı |
| Kurulum aracı | Velopack (MIT) | 0 | WiX kullanılırsa OSMF $10/ay (<20 çalışan) |
| PDF rapor (Faz 5) | QuestPDF Community | 0 (yıllık gelir < $1M) | Üzerinde $1.999/yıl |
| Bulut yedek API'leri | Google/Microsoft/Dropbox | 0 (kotalar satıcının uygulama projesine yazılır; Google'da 2026'dan itibaren günlük faturalama eşiği var) | `drive.file` non-sensitive → CASA denetimi gerekmez |
| WhatsApp durum aktarıcısı | **Yapılmayacak** (satıcı sunucusunda mesaj trafiği tutulmaz) | 0 | Gerekirse işletmenin kendi aktarıcısı / BSP |

## 3. Toplam sahip olma maliyeti — lisans seçenekleri (yıllık, kabaca)
| Seçenek | Doğrudan | Bakım/operasyon | Risk |
|---|---|---|---|
| Kendi API (seçilen) | VPS + alan adı (tahmin: düşük yüzlerce €) | Orta: güvenlik güncellemeleri, yedek, izleme bizde (aynı C# yığını) | Bağımlılık yok |
| Keygen Cloud | $0 → $990 | Düşük | SaaS bağımlılığı, USD |
| Keygen CE | VPS (Rails + Postgres + Redis, ~500 MB+ RAM) | Yüksek: Ruby/Redis bilgisi | Operasyon yükü |
| Cryptolens | €299/ay → ~€3.600/yıl | Düşük | Yüksek sabit maliyet, biçim bağımlılığı |

## 4. Fiyatlandırma ilkeleri (öneri)
- TL cinsinden, öngörülebilir kademeler (personel/şube sayısı); temel özellikler (kaynak takvimi, paket, yetki, yedek, dışa aktarma) her pakette.
- Pazar yeri komisyonu yok.
- Deneme bitince otomatik ücretlendirme yerine açık onay; tek adımda iptal.
- Lisans bitince veri kilitlenmez (salt-okunur), dışa aktarma her zaman serbest.
- Mesaj ve bulut maliyeti işletmenin kendi hesabında; Vernuko aracı kâr marjı eklemez.
