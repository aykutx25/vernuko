# D. Lisans Altyapısı Kararı

Ayrıntılı ve kaynaklı karşılaştırma: [research/licensing-payments-signing.md](research/licensing-payments-signing.md).

## Kaynak kod lisansı (2026-11-12 kararı)
Vernuko'nun kaynak kodu **AGPL-3.0-only** ile açılır (`LICENSE`). Sahibin kararı; gerekçe ve sonuçlar:

| Konu | Sonuç |
|---|---|
| Satış | Serbest. Satılan şey: hazır ve güncel kurulum paketi, otomatik güncelleme, kurulum/taşıma desteği ve sunucu gerektiren hizmetler (bulut yedek, SMS/WhatsApp, lisans/etkinleştirme). |
| Rakip ürünler | AGPL gereği, kodu değiştirip dağıtan ya da ağ üzerinden sunan herkes kendi kaynak kodunu da aynı lisansla açmak zorundadır; kapalı rakip ürün çıkaramaz. |
| Lisans kontrolü | Kodda görünür ve değiştirilebilir. Gerçek koruma sunucu tarafındadır: etkinleştirme, güncelleme izni ve ücretli hizmetler geçerli lisans ister (Faz 4–6). Uygulamadaki imzalı lisans belgesi sahte lisans üretilmesini engellemeye devam eder. |
| Ad ve logo | "Vernuko" adı/logosu AGPL kapsamında değildir; marka tescili (TÜRKPATENT) önerilir. |
| Çift lisans | Yapılmıyor (AGPL dışında ticari kod lisansı satılmıyor). Bu, ücretsiz açık kaynak imza programlarının (SignPath Foundation) koşuludur. |
| Bileşenler | Hepsi MIT/Apache-2.0, AGPL-3.0 ile uyumlu (`THIRD-PARTY-NOTICES.md`). |
| Hukuki kontrol | ⚖️ Lisans seçimi, marka kullanım politikası ve kullanıcı sözleşmesi bir avukatla gözden geçirilmelidir. |

## Karar özeti
| Konu | Karar | Gerekçe |
|---|---|---|
| Lisans servisi | **Kendi küçük lisans API'miz** (ASP.NET Core minimal API, .NET 10 LTS, tek süreç, PostgreSQL veya SQLite, tek küçük VPS). Lisans belgeleri **ECDSA P-256 / SHA-256** ile imzalanır. Yedek plan: Keygen Cloud Std 1. | En düşük toplam sahip olma maliyeti; ekip tek dilde (C#) kalır; Redis/Rails işletme yükü yok; ödeme webhook'larını zaten bizim tüketmemiz gerekiyor; dış sağlayıcıya bağımlılık yok. |
| İstemci doğrulama | **Uygulandı (Faz 1):** `Vernuko.Licensing` — imzalı belge doğrulama, çevrimdışı tolerans, saat geri alma koruması, salt-okunur mod. | Özel anahtar uygulamada yok; yalnızca açık anahtarlar gömülür, anahtar kimliğiyle döndürülebilir. |
| Ödeme altyapısı | **Uluslararası: Paddle (Merchant of Record). Yurt içi TL abonelik: iyzico Subscription.** Stripe yalnızca yurt dışı şirket kurulursa (ör. Stripe Atlas). | **Stripe, Türkiye'de kayıtlı işletmelere açık değil** (stripe.com/global listesinde Türkiye yok; Managed Payments da TR'yi desteklemiyor). Paddle Türk satıcıyı kabul ediyor ve KDV/VAT yükünü üstleniyor (%5 + 50¢). |
| Kod imzalama | **Bulut HSM'li OV sertifikası** (ör. Certum SimplySign veya SSL.com eSigner). | Microsoft Artifact Signing Türkiye'deki kuruluşlara açık değil. 2023'ten beri özel anahtar donanımda olmak zorunda; EV sertifikası 2024'ten beri SmartScreen'i otomatik geçmiyor. |

> ⚠️ **Talimattaki "tercih edilen ödeme altyapısı: Stripe" maddesi doğrudan uygulanamıyor.** Türkiye merkezli bir satıcı Stripe hesabı açamıyor. Yukarıdaki seçenekler arasında karar sizin: (1) Paddle + iyzico (önerilen, ek şirket gerektirmez), (2) ABD şirketi + Stripe (Atlas kurulum $500 + yıllık $100; Form 5472 yükümlülüğü, eksik bildirimde $25.000 ceza; MoR değil, vergiyi siz yönetirsiniz). Lisans sunucusu ödeme sağlayıcısından soyutlanacak şekilde tasarlandı (webhook adaptörü), bu yüzden karar Faz 6'ya kadar ertelenebilir.

## Seçenek karşılaştırması
| Kriter | Keygen CE (self-host) | Keygen Cloud | Cryptolens (Devolens) | Kendi API'miz |
|---|---|---|---|---|
| Lisans | FCL-1.0-ALv2: kendi ürününüz için ticari self-host serbest | SaaS | SaaS, .NET SDK BSD-3 | Kendi kodumuz |
| Fiyat (resmi sayfa, Kasım 2026) | Yazılım ücretsiz + sunucu | 100 aktif kullanıcıya kadar ücretsiz; 1.000 için $99/ay veya $990/yıl | €299/ay (5.000 lisans) | Geliştirme emeği + küçük VPS (tahmin, doğrulanmadı) |
| Kaynak kullanımı | Ruby/Rails + PostgreSQL ≥13 + Redis ≥6.2 + Sidekiq; ~500 MB RAM önerisi | Sunucu yok | Sunucu yok | Tek .NET süreci (tahmini 100–200 MB RAM) |
| Offline lisans | Var (TTL'li imzalı dosya) | Var | Var (RSA imzalı dosya) | Var (Faz 1'de istemci tarafı hazır) |
| Bakım yükü | Yüksek (Ruby/Redis güncellemeleri, 6 aylık sürümler) | Düşük | Düşük | Orta (güvenlik sorumluluğu bizde) |
| Bağımlılık | Düşük (2 yıl sonra Apache-2.0) | Orta-yüksek | Orta-yüksek | Yok |

**Neden Keygen CE değil?** Yazılım ücretsiz ama C# ekibine Ruby/Rails/Redis işletme yükü getiriyor; lisans servisinin küçük kapsamı için bu yük orantısız. **Neden Cryptolens değil?** €299/ay giriş fiyatı küçük bir ISV için yüksek ve SDK/biçim bağımlılığı yaratıyor. **Keygen Cloud** ilk aylarda (100 kullanıcı altı ücretsiz) hızlı bir başlangıç seçeneği olarak kalır; istemci tarafı belge doğrulayıcı bu durumda Keygen'in Ed25519 dosyaları için bir adaptörle genişletilir (NSec veya BouncyCastle, MIT).

## Lisans belgesi (uygulandı)
`src/Vernuko.Licensing/LicenseDocument.cs`:
```
{ "kid": "2026-01", "alg": "ES256",
  "payload": base64url({ "v":1, "lid":…, "prd":"vernuko-desktop", "ed":"Standart", "feat":[…], "aid":…,
                         "dev": <cihaz özeti>, "max":1, "iat":…, "exp": <abonelik sonu>,
                         "rfr": <yenileme zamanı>, "grace": 14 }),
  "sig": base64url(ECDSA-P256-SHA256(payload)) }
```
- **Cihaz kimliği:** Windows `MachineGuid` + tuz → SHA-256 özeti (ham donanım bilgisi gönderilmez). RAM/disk/ekran kartı değişiminde değişmez; yalnızca Windows yeniden kurulumunda değişir → yeniden etkinleştirme (müşteri kilitlenmez, salt-okunur moda geçer).
- **Çevrimdışı çalışma:** `rfr` zamanına kadar tam erişim; sonrasında `grace` gün daha tam erişim ("Lisans X gün içinde çevrim içi doğrulanmalı" uyarısı); sonra salt-okunur.
- **Saat geri alma:** Gözlenen en yüksek zaman (`license.high_water_mark_utc`) saklanır; etkin zaman = max(sistem saati, yüksek su işareti). Saati geri almak lisansı uzatamaz, ama tek başına kilitlemez de (48 saatlik tolerans; yanlış pozitif yok).
- **Deneme:** 30 gün, kurulumda başlar. Not: yerel deneme sayacı kurcalanmaya dayanıklı değildir; sunucu tarafı deneme kaydı Faz 6'da.

## Lisans süresi bittiğinde (uygulandı ve test edildi)
- Veriler **asla silinmez, şifrelenmez veya kilitlenmez**.
- Uygulama **salt-okunur moda** geçer: görüntüleme, arama, rapor, **yedek alma, geri yükleme, dışa aktarma** serbest; yeni randevu/müşteri/ödeme kaydı engellenir (`Error.ReadOnlyMode`).
- Lisans sunucusu kapalıysa geçerli müşteriler `grace` süresi boyunca etkilenmez.
- Testler: `Expired_license_is_read_only_but_data_stays_readable_and_exportable`, `Hardware_change_keeps_data_and_switches_to_read_only_until_reactivation`, `Offline_use_works_until_grace_ends_then_read_only`, `Clock_rollback_cannot_extend_validity_but_does_not_lock_by_itself`.

## Lisans sunucusu (Faz 6 — uygulandı)
**Karar (2026-11-13): WordPress + WooCommerce eklentisi** (`wordpress/vernuko-license`). Satış, ödeme (iyzico/PayTR/havale gibi WooCommerce ödeme eklentileri), anahtarın e-postayla gönderimi, Hesabım › Lisanslarım, yenileme ve iade sitenin içinde; ayrı sunucu yok. Aşağıdaki .NET sunucusu aynı protokolle yedek seçenek olarak kalır.

Ayrıntı, kurulum ve işletme: [LICENSE_SERVER.md](LICENSE_SERVER.md). Özet:
- `src/Vernuko.LicenseServer`: ASP.NET Core minimal API + SQLite, tek süreç, TLS ters vekil arkasında. **Müşterinin randevu/müşteri verisi bu sunucuya gönderilmez.**
- Etkinleştirme (anahtar + özetlenmiş cihaz kimliği), haftalık yenileme, "bu bilgisayardan kaldır" (taşıma), cihaz sınırı, sunucu tarafı deneme kaydı (yeniden kurulumla deneme baştan başlamaz), güncelleme izni (Velopack dosyaları yalnızca etkin aboneliğe).
- Paddle Billing webhook'u: `Paddle-Signature` HMAC-SHA256 doğrulama, olay kimliğiyle idempotent, `occurred_at` ile sıra dışı olaylara dayanıklı. Bilinmeyen fiyat lisans üretmez.
- Ödeme toleransı 7 gün + çevrimdışı 14 gün; iptal ödenen dönemi kısaltmaz.
- Masaüstü: Ayarlar › Lisans'ta anahtarla etkinleştirme, "Şimdi doğrula", "Bu bilgisayardan kaldır". Sunucu adresi ve açık anahtarlar derleme anında sabitlenir (yerel bir ayarla başka sunucuya yönlendirilemez).
- ⚠️ Gerçek Paddle hesabıyla denenmedi; iyzico webhook'u yok (TL satışı elle lisans); anahtarın e-postayla gönderimi yok.

## Lisans türleri (planlanan)
Deneme · aylık · yıllık · tek cihaz · çok cihaz · işletme · çok şube · kurumsal. Özellik yetkileri belgede `feat` listesiyle taşınır; çekirdek operasyon (randevu, müşteri, paket, yetki, yedek, dışa aktarma) hiçbir pakette kısıtlanmaz.
