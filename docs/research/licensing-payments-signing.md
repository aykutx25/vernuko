> **Araştırma notu.** Bu rapor 11–12 Kasım 2026'da resmi web siteleri, yardım merkezleri, fiyat sayfaları ve herkese açık kullanıcı yorumları taranarak hazırlanmıştır. Her iddia kaynağıyla birlikte "doğrulandı" / "doğrulanamadı" olarak işaretlenmiştir. Fiyatlar ve politikalar sık değişir; satın alma veya hukuki karar öncesinde ilgili kaynak yeniden kontrol edilmelidir. Hukuki konular avukat görüşü yerine geçmez.

# Vernuko: Lisanslama, Ödeme, Kod İmzalama ve Dağıtım Teknik Durum Tespiti

**Kapsam:** Vernuko, Türkiye merkezli bir satıcının kendi web sitesinden sattığı ticari bir Windows masaüstü uygulamasıdır (C#/.NET, WPF, SQLite, offline-first).
**Araştırma tarihi:** 2026-11-11/12.
**İşaretleme kuralı:** Her olgusal iddiada kaynak verilmiştir. "(doğrulandı: URL)" iddianın o resmi sayfada görüldüğünü, "(doğrulanamadı)" ise resmi bir kaynakta teyit edilemediğini gösterir. Kaynağa dayanmayan öneri ve yorumlar **Değerlendirme** olarak etiketlenmiştir. Fiyatlar yalnızca resmi sayfada (veya o sayfanın veri kaynağında) görüldüyse verilmiştir.

---

## 0. Karar için kritik bulgular

1. **Stripe, Türkiye'de kayıtlı işletmelere açık değil.** stripe.com/global sayfasındaki 51 ülke/bölgelik listede Türkiye yok (doğrulandı: https://stripe.com/global). Stripe'ın MoR ürünü Managed Payments'ın desteklediği işletme konumları arasında da TR bulunmuyor (doğrulandı: https://docs.stripe.com/payments/managed-payments/eligibility).
2. **Keygen CE ticari self-host için ücretsiz.** Lisans FCL-1.0-ALv2 (Fair Core License). Yasak olan tek şey "Competing Use", yani Keygen'in yerine geçen bir hizmeti başkalarına sunmak. README açıkça "free (as in beer) to self-host for personal and commercial use" diyor (doğrulandı: https://github.com/keygen-sh/keygen-api/blob/master/LICENSE.md ve https://github.com/keygen-sh/keygen-api/blob/master/README.md).
3. **Microsoft Artifact Signing Türkiye için kullanılamıyor.** Public Trust sertifikaları şu bölgelerdeki kuruluşlara açık: ABD, Kanada, AB, Birleşik Krallık, Avustralya, Yeni Zelanda, Japonya, Güney Kore, Singapur, İsviçre, Norveç ve İsrail. Bireysel geliştiriciler yalnızca ABD ve Kanada'dan başvurabiliyor (doğrulandı: https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart). Türkiye bu listede yok. Pratik çözüm, bulut HSM'li bir OV sertifikası.
4. **WiX Toolset ikili sürümleri için Open Source Maintenance Fee (OSMF) ödenmesi gerekiyor.** Yıllık brüt geliri 10.000 USD veya üzerinde olan ve WiX'i gelir getiren bir işte kullanan kuruluşlar için geçerli. Ücret 20 çalışandan az kuruluşlar için **10 USD/ay**, 20-100 çalışan için 40 USD/ay, 100'den fazla çalışan için 60 USD/ay (doğrulandı: https://github.com/wixtoolset/wix/blob/main/OSMFEULA.txt ve https://github.com/sponsors/wixtoolset). EULA kabulü WiX v7'de zorunlu hale geldi (`-acceptEula wix7`) (doğrulandı: https://docs.firegiant.com/wix/osmf/).
5. **.NET 8 ve .NET 9 desteği 10 Kasım 2026'da sona erdi.** .NET 10 LTS 14 Kasım 2028'e kadar destekleniyor (doğrulandı: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
6. **.NET 10 ve .NET 11 BCL'inde Ed25519 yok.** .NET 11 yalnızca X25519 (anahtar değişimi) ekledi (doğrulandı: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries). ECDSA P-256 ise yerleşik olarak var.

---

## 1. Lisanslama seçenekleri

### 1.1 Keygen CE (self-hosted)

**Lisans:** keygen-api deposundaki LICENSE.md, "Fair Core License, Version 1.0, ALv2 Future License (FCL-1.0-ALv2)" lisansıdır. Yazılım "Competing Use" dışındaki her amaçla kullanılabilir. Competing Use şöyle tanımlanıyor: yazılımı başkalarına, yazılımın yerine geçen veya "the same or substantially similar functionality" sunan ticari bir ürün ya da hizmet olarak sağlamak. "Permitted Purposes" listesinde "for your internal use and access" açıkça yer alıyor (doğrulandı: https://github.com/keygen-sh/keygen-api/blob/master/LICENSE.md).
- **Ek kısıt:** Lisans anahtarı işlevi taşınamaz, değiştirilemez veya atlatılamaz. Bu kısıt EE özelliklerinin kilidini korur (doğrulandı: aynı URL).
- **Gelecek lisans:** Her sürüm, yayımlandığı tarihten 2 yıl sonra Apache-2.0 lisansına geçer (doğrulandı: aynı URL).
- **Ticari kullanım:** README'de şu ifadeler var: "Keygen CE ... is free (as in beer) to self-host for personal and commercial use." Ayrıca "You can... self-host Keygen EE to license your enterprise applications... embed Keygen CE in your on-premise applications" (doğrulandı: https://github.com/keygen-sh/keygen-api/blob/master/README.md).
- **Vernuko için sonuç (Değerlendirme):** Kendi ürününüzü lisanslamak için CE'yi self-host etmek izin verilen kullanımdır. Yasak olan, Keygen'i başkalarına lisanslama hizmeti olarak satmaktır.

**Yığın:** Keygen bir Ruby on Rails uygulamasıdır. Web sunucusu Puma, arka plan işleri Sidekiq'tir. Dokümanlara göre gereksinimler Ruby ≥ 3.1, PostgreSQL ≥ 13 ve Redis ≥ 6.2 (doğrulandı: https://keygen.sh/docs/self-hosting/). Mevcut Gemfile'da `ruby '3.4.12'`, `rails ~> 8.1.4.1` ve `sidekiq ~> 7.3` sabitlenmiş (doğrulandı: https://github.com/keygen-sh/keygen-api/blob/master/Gemfile). Kurulum Docker ile yapılır.

**Donanım:** x86_64 ve SSE 4.2 destekli bir CPU gerekiyor. Belgede şu ifade var: "We recommend using a minimum of 500MB of RAM, but exact requirements will depend on configuration." Çekirdek sayısı belirtilmemiş (doğrulandı: https://keygen.sh/docs/self-hosting/). PostgreSQL ve Redis'in bellek ihtiyacı bu rakama dahil mi, belli değil (doğrulanamadı).

**Yalnızca EE'de bulunan özellikler:** request logs, event logs, environments, permissions, import/export, OCI/Docker registry, SSO/SAML, ClickHouse desteği ve multiplayer (çoklu hesap) modu. EE çalışmak için lisans anahtarı ister. CE desteği Discord ve forum üzerinden "best-effort" olarak verilir (doğrulandı: https://keygen.sh/docs/self-hosting/). CE sürümleri yaklaşık 6 ayda bir çıkar ("LTS" mantığı) (doğrulandı: README). EE "singleplayer" fiyatı sayfada yazmıyor (doğrulandı: https://keygen.sh/pricing/).

### 1.2 Keygen Cloud

**Fiyatlar** (keygen.sh/pricing sayfası planları `api.keygen.sh/v1/plans` uç noktasından dinamik olarak yüklüyor; aşağıdaki değerler bu resmi veri kaynağından alındı ve sent cinsinden çevrildi) (doğrulandı: https://api.keygen.sh/v1/plans?limit=100):

| Plan | Aylık | Yıllık | ALU / lisans limiti |
|---|---|---|---|
| Dev | 0 $ | 0 $ | 100 (1 ürün, 10 policy) |
| Std 1 | 99 $ | 990 $ | 1.000 (5 ürün, 5 admin) |
| Std 2 | 299 $ | 2.990 $ | 10.000 |
| Std 3 | 599 $ | 5.990 $ | 25.000 |
| Std 4 | 999 $ | 9.990 $ | 100.000 |
| Std 5 | 1.999 $ | 19.990 $ | 250.000 |
| Ent 1 / 2 / 3 | 2.999 / 6.999 / 9.999 $ | 29.990 / 69.990 / 99.990 $ | 250k / 500k / 1M |

- **ALU tanımı:** "Active licensed user", son 90 gün içinde aktivitesi olan lisanslı kullanıcıdır. Aktivite şunları kapsar: lisans doğrulama, cihaz aktivasyonu veya deaktivasyonu, entitlement/meter tüketimi ve offline check-in/check-out. Deneme lisansları da sayılır, ancak 90 gün hareketsiz kalınca düşer (doğrulandı: https://keygen.sh/pricing/).
- **Limit aşımı:** Dev ve deneme hesaplarında limitler katıdır ve aşımda `402` döner. Ücretli planlarda limitler yumuşaktır, aşımda e-posta ile bildirim yapılır (doğrulandı: https://keygen.sh/pricing/).
- **Ek ücretli seçenekler:** Whitelabel alan adı 995 $/yıl, Premium Support SLA 995 $/ay (doğrulandı: https://keygen.sh/pricing/).

**Kriptografi ve offline:**
- **İmza şemaları:** Lisans anahtarı ve dosyaları şu şemalarla imzalanabiliyor: `ED25519_SIGN` (önerilen), `ECDSA_P256_SIGN` (FIPS gerekiyorsa), `RSA_2048_PKCS1_PSS_SIGN_V2`, `RSA_2048_PKCS1_SIGN_V2` ve `RSA_2048_JWT_RS256` (doğrulandı: https://keygen.sh/docs/api/cryptography/).
- **Şifreleme:** Lisans ve makine dosyaları isteğe bağlı olarak AES-256-GCM ile şifrelenebiliyor. Lisans dosyasında anahtar, lisans anahtarının SHA-256 özetinden türetiliyor. Makine dosyasında lisans anahtarı ile parmak izi birleştirilip SHA-256'dan geçiriliyor. `alg` alanı örneğin `aes-256-gcm+ed25519` olur (doğrulandı: aynı URL).
- **Makine dosyası TTL'i:** Varsayılan TTL 1 ay, en az 1 saat. `ttl=null` değeri "perpetual and irrevocable" anlamına gelir (doğrulandı: https://keygen.sh/docs/api/machines/).

**Makine aktivasyonu ve heartbeat:**
- **Aktivasyon:** `fingerprint` ve lisans ilişkisi ile `POST /machines` çağrılır. Parmak izi lisans kapsamında benzersiz olmalıdır (doğrulandı: https://keygen.sh/docs/api/machines/).
- **Heartbeat:** Varsayılan pencere son ping'den itibaren 10 dakikadır ve policy'deki `heartbeatDuration` ile değiştirilebilir. Ping kesilirse makine otomatik deaktive edilir (silinir). Ölü makineler 2 dakikada bir temizlenir (doğrulandı: aynı URL).

**Entitlement:** Policy'ye bağlanan entitlement o policy'deki tüm lisanslara uygulanır. Lisansa doğrudan da bağlanabilir. Release'lere kısıt olarak da eklenebilir (doğrulandı: https://keygen.sh/docs/api/entitlements/). Makine dosyasına `include` ile entitlement gömülebilir (doğrulandı: https://keygen.sh/docs/api/machines/).

**Stripe entegrasyonu:**
- **Hazır entegrasyon yok:** Keygen'de yerleşik bir Stripe entegrasyonu bulunmuyor. Keygen'in önerdiği yollar webhook tabanlı örnek uygulamalar ve Zapier (no-code) (doğrulandı: https://keygen.sh/pricing/ ve https://github.com/keygen-sh/example-stripe-integration).
- **Örnek uygulamanın sınırları:** Örnek Node.js ile yazılmış ve "not 100% production-ready" olarak etiketlenmiş. Abonelik iptalinde lisans iptali uygulanmamış (doğrulandı: aynı repo README).
- **.NET desteği:** Resmi bir .NET SDK'sı yok. NSec ve BouncyCastle ile Ed25519 + AES-256-GCM lisans dosyası doğrulayan MIT lisanslı bir C# örneği var (doğrulandı: https://github.com/keygen-sh/example-csharp-cryptographic-license-files).

### 1.3 Cryptolens (Devolens markası)

**Marka ve fiyatlar:** cryptolens.io/pricing adresi devolens.com/pricing'e yönlendiriyor. Sayfadaki ifadeler: "Cryptolens is our comprehensive software licensing system" ve "Devolens is a newly created brand name" (doğrulandı: https://devolens.com/pricing). Planlar:

| Plan | Aylık ücret | Aktif lisans | API isteği/ay | Admin | Destek |
|---|---|---|---|---|---|
| Business | **299 €** | 5.000 | 250.000 | 5 | E-posta, SLA yok |
| Scale | **699 €** | 10.000 | 1.000.000 | Sınırsız | Öncelikli, standart SLA |
| Enterprise | Teklif | Sınırsız | Sınırsız | Sınırsız + RBAC | Özel SLA |
| Deneme sonrası ücretsiz | 0 | 10 | 1 istek/10 sn | 1 | — |

- **Plan koşulları:** 30 günlük sınırsız deneme var. Business planı en fazla 250 çalışanı ve 50 milyon $ geliri olan şirketler içindir. Ayrıca bir "Startup program" mevcut (doğrulandı: aynı URL).
- **Yıllık fiyat:** Sayfada yıllık fiyat yazmıyor (doğrulandı: aynı URL).

**.NET SDK:**
- **Paket:** NuGet paketi `Cryptolens.Licensing`, son sürümü 4.0.54 (8/6/2026). Hedefleri .NET Standard 2.0 ve .NET Framework 3.5-4.8 (doğrulandı: https://www.nuget.org/packages/Cryptolens.Licensing).
- **Lisans:** Paket içindeki LICENSE dosyası BSD 3-Clause metni ("Copyright (c) 2011-2022, Cryptolens AB"). nuspec'te `requireLicenseAcceptance=true` (doğrulandı: nupkg içeriği ve https://github.com/Cryptolens/cryptolens-dotnet/).

**Offline kullanım:**
- **İmzalı dosya:** Activate veya GetKey yanıtı RSA ile imzalı bir "license file" olarak kaydedilir (`SaveToFile`). Doğrulama `LoadFromFile(..., RSAPubKey)` ve `HasValidSignature(RSAPubKey, 365).IsOnRightMachine(...)` ile yapılır. Yaş, imzalı `SignDate` alanından ölçülür (doğrulandı: https://help.cryptolens.io/examples/offline-verification).
- **Air-gapped aktivasyon:** USB ile aktivasyon dosyası taşınabilir (doğrulandı: aynı URL).
- **Saat sınırlaması:** Doküman, saat geri alınırsa eski dosyanın geçebileceğini kendisi belirtiyor (doğrulandı: aynı URL).

**Floating lisans:** Her süreç rastgele bir ID ile koltuk alır ve `FloatingTimeInterval` penceresi içinde yenilemesi gerekir. `MaxOverdraft` ile limit üstü kullanıma izin verilebilir. Offline ortamda koltuk koordinasyonu için "License Server" gerekir (doğrulandı: https://help.cryptolens.io/licensing-models/floating).

**Veri yerleşimi:** Business ve Scale planlarında veri "EU-based infrastructure" üzerinde tutulur. Enterprise planında özel bölge seçilebilir (doğrulandı: https://devolens.com/pricing). Self-hosted sunucu sunulmuyor, yalnızca "License Server" eklentisi var (doğrulandı: aynı URL).

### 1.4 Kendi lisans API'niz (self-built)

**Kripto gerçekleri:**
- **Ed25519:** .NET 10 BCL'inde **yok**. .NET 10 kripto yenilikleri ML-KEM, ML-DSA, SLH-DSA, Composite ML-DSA ve AES KeyWrap'tir. ML-DSA ve SLH-DSA `[Experimental]` işaretli ve OpenSSL 3.5+ ya da PQC destekli Windows CNG ister (doğrulandı: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries). .NET 11'de de Ed25519 yok, yalnızca `X25519DiffieHellman` eklendi (doğrulandı: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries).
- **Ed25519'un BCL'deki durumu:** dotnet/runtime #14741 numaralı konu 27 Nisan 2026'da kapatıldı ve takip #63174'e taşındı. Yayın hedefi yok. Bakımcının 2023 yorumuna göre plan Windows'ta EdDSA desteği olmaması nedeniyle askıda (doğrulandı: https://github.com/dotnet/runtime/issues/14741).
- **ECDSA P-256:** Yerleşik olarak var: `ECDsa` ve `ECCurve.NamedCurves.nistP256` (doğrulandı: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.ecdsa?view=net-10.0).
- **Ed25519 kütüphaneleri:**
  - **NSec.Cryptography:** 26.4.0, MIT lisanslı, libsodium tabanlı (libsodium ≥1.0.22 <1.0.23 bağımlılığı var), net9.0 ve üzeri (doğrulandı: https://www.nuget.org/packages/NSec.Cryptography). libsodium NuGet paketi ISC lisanslı (doğrulandı: https://www.nuget.org/packages/libsodium).
  - **BouncyCastle.Cryptography:** 2.7.0, MIT, tamamen yönetilen kod, net6.0/netstandard2.0/net461 (doğrulandı: https://www.nuget.org/packages/BouncyCastle.Cryptography).

**Asgari güvenli tasarım (Değerlendirme, kaynaklı parçalar işaretli):**
1. **Sunucu:** ASP.NET Core minimal API. Yerleşik `Microsoft.AspNetCore.RateLimiting` ara katmanı fixed window, sliding window, token bucket ve concurrency limitörleri sunar. Sayaçlar instance başına bellekte tutulur (doğrulandı: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit).
2. **Veritabanı:** Tek instance ve düşük hacim için SQLite yeterlidir. Ödeme olayları, eşzamanlı yazma ve yedekleme ihtiyacı arttıkça PostgreSQL tercih edilmelidir. Tablolar: customers, licenses, activations (fingerprint), entitlements, processed_events (webhook event ID'si üzerinde UNIQUE), audit_log.
3. **Lisans belgesi:** Kanonik JSON alanları `licenseId`, `product`, `edition`, `entitlements[]`, `fingerprint`, `issuedAt`, `expiresAt`, `graceUntil` ve `kid` olur. İmza **ECDSA P-256/SHA-256** ile atılır (BCL, sıfır bağımlılık) veya Ed25519 ile (NSec ya da BouncyCastle). İstemciye yalnızca açık anahtar(lar) gömülür. Anahtar rotasyonu için `kid` kullanılır.
4. **Özel anahtar:** Web sunucusunda düz dosya olarak tutulmamalıdır. Ayrı bir imza servisi veya KMS/HSM kullanılmalıdır. KMS'lerin P-256 desteği ayrıca doğrulanmadı (doğrulanamadı).
5. **Offline-first:** Belge yerelde doğrulanır. Periyodik çevrimiçi yenileme yapılır (ör. 30 gün TTL + 14 gün tolerans). Saat geri alınmasına karşı "son görülen zaman" kalıcı olarak tutulur. Cryptolens'in belgelediği saat sorunu bu tasarım için de geçerlidir.
6. **Ödeme bağlantısı:** Paddle, Stripe veya iyzico webhook'u gelir, imza doğrulanır, olay kuyruğa alınır, sonra lisans oluşturulur, uzatılır veya askıya alınır. Ayrıntılar §2.4'te.

### 1.5 Karşılaştırma tablosu

| Kriter | Keygen CE (self-host) | Keygen Cloud | Cryptolens | Kendi API'niz |
|---|---|---|---|---|
| **Fiyat** | Yazılım ücretsiz (doğrulandı), sunucu maliyeti size ait | Dev 0 $ (100 ALU), Std 1 99 $/ay veya 990 $/yıl (1.000 ALU), Std 2 299 $/ay (10k) (doğrulandı) | Business 299 €/ay (5.000 aktif lisans), Scale 699 €/ay (doğrulandı) | Geliştirme emeği + küçük VPS (Değerlendirme) |
| **Güvenlik** | Ed25519/ECDSA/RSA imzalı, AES-256-GCM şifreli dosyalar (doğrulandı). Sunucu yamaları ve sertleştirme sizde | Aynı kripto, işletim Keygen'de | RSA imzalı lisans dosyaları, BSD-3 SDK (doğrulandı) | Tasarıma bağlı. ECDSA P-256 BCL'de, Ed25519 için 3. parti gerekir (doğrulandı) |
| **Kaynak kullanımı** | Docker, x86_64/SSE4.2, en az ~500 MB RAM önerisi + PostgreSQL ≥13 + Redis ≥6.2 + Sidekiq (doğrulandı) | Sunucu yok | Sunucu yok (SaaS) | Tek ASP.NET Core süreci + SQLite veya PostgreSQL (Değerlendirme) |
| **Offline destek** | Lisans ve makine dosyası checkout, TTL'li (doğrulandı) | Aynı (doğrulandı) | İmzalı lisans dosyası, USB aktivasyonu. Floating için License Server (doğrulandı) | Kendi belge formatınız (Değerlendirme) |
| **Bakım yükü** | Yüksek: Ruby/Rails/Postgres/Redis güncellemeleri, 6 aylık CE sürümleri, best-effort destek (doğrulandı) | Düşük | Düşük | Orta: aynı C# yığını, ama güvenlik sorumluluğu tamamen sizde |
| **Bağımlılık / kilitlenme** | Düşük. FCL lisanslı, 2 yıl sonra Apache-2.0'a geçer (doğrulandı). Ekipte Ruby bilgisi gerekir | Orta-yüksek (SaaS, ABD şirketi). Offline dosyalar çalışma anı bağımlılığını azaltır | Orta-yüksek (SaaS, AB'de veri, SDK formatı) (doğrulandı) | Yok |

**Değerlendirme:** Erken aşamada ve 1.000 ALU'nun altında en düşük riskli yollar şunlar: (a) kendi küçük C# API'niz, ECDSA P-256 imzalı belgelerle, veya (b) Keygen Cloud Std 1 (990 $/yıl). Keygen CE, C# ağırlıklı bir ekibe Ruby/Redis işletme yükü getirir. Cryptolens'in 299 €/ay giriş fiyatı küçük bir ISV için yüksek.

---

## 2. Türkiye merkezli satıcı için ödemeler

### 2.1 Stripe

- **Doğrudan hesap:** Türkiye stripe.com/global listesinde yok. Liste 51 ülke/bölge içeriyor ve sayfada "Türkiye/Turkey" ifadesi geçmiyor (doğrulandı: https://stripe.com/global).
- **Managed Payments (MoR):** İşletme konumu yalnızca CA, US, AB ve AEA ülkeleri, CH, GB, AU, HK, JP ve SG olabiliyor. TR listede yok (doğrulandı: https://docs.stripe.com/payments/managed-payments/eligibility).
- **Stripe Atlas (yabancı tüzel kişi yolu):**
  - **Ücretler:** 500 $ tek seferlik kuruluş ücreti (ilk yıl registered agent dahil), sonra yıllık 100 $ (doğrulandı: https://stripe.com/atlas).
  - **Şirket türü ve süreç:** Delaware C corp veya LLC kurulur. Kuruluş 1-2 iş günü sürer. SSN'i olmayan kurucu için EIN 5-35 iş günü sürebilir. Delaware franchise vergisi ve raporu her yıl 1 Mart'a kadar verilmelidir. "Atlas can't guarantee that your business will be approved to use Stripe payments" (doğrulandı: https://docs.stripe.com/atlas/signup).
  - **Uygunluk:** "Apply online from almost anywhere in the world" deniyor (doğrulandı: https://stripe.com/atlas). Ancak Türkiye'deki kuruculara özel uygunluk resmi bir listede teyit edilemedi (doğrulanamadı).
  - **ABD vergi yükümlülüğü:** Tamamen yabancı sahipli tek üyeli LLC (disregarded entity), pro forma Form 1120 ekiyle Form 5472 vermek zorundadır. Vermeme cezası **25.000 $** ve ihbardan 90 gün sonra her 30 gün için ek 25.000 $ (doğrulandı: https://www.irs.gov/instructions/i5472).
  - **Türkiye vergi tarafı:** KVK/kontrollü yabancı kurum ve KDV etkileri bu çalışmada incelenmedi (doğrulanamadı). Mali müşavir görüşü şart.
  - **Değerlendirme:** Atlas yolunda MoR olmadığı için AB KDV'si ve ABD sales tax yükümlülükleri satıcıda kalır.

### 2.2 Merchant of Record (MoR) sağlayıcıları

| Sağlayıcı | Türkiye'deki satıcıyı kabul ediyor mu? | Vergi/KDV | Ücret |
|---|---|---|---|
| **Paddle** | Evet. Desteklenmeyen tedarikçi ülkeler listesinde Türkiye yok ("works with software businesses anywhere in the world with the exception of...") (doğrulandı: https://www.paddle.com/help/start/account-verification/which-countries-are-supported-by-paddle). Paddle'ın Eylül 2026 tarihli blog yazısı "based in Turkey and looking to sell into other international markets" diyor (doğrulandı: https://www.paddle.com/blog/payments-in-turkey) | MoR olarak "global tax and regulatory compliance" sağlıyor (doğrulandı: https://www.paddle.com/pricing) | **5% + 50¢** / Checkout işlemi (doğrulandı: aynı URL). TR banka hesabına ödeme ayrıntıları (doğrulanamadı) |
| **Lemon Squeezy** | Türkiye banka ödemesi listesinde yer alıyor (doğrulandı: https://docs.lemonsqueezy.com/help/getting-started/supported-countries) | MoR | **5% + 50¢**. ABD dışı işlemlerde ek ücret çıkabilir (doğrulandı: https://www.lemonsqueezy.com/pricing) |
| **FastSpring** | Yasaklı ülke listesinde Türkiye yok (doğrulandı: https://fastspring.com/terms-use/restricted-countries) | KDV/sales tax'ı tahsil edip yatırıyor (doğrulandı: https://fastspring.com/pricing/) | Özel teklif, hacme bağlı. Sabit oran yayımlanmıyor (doğrulandı: aynı URL) |

**Lemon Squeezy uyarısı:** Lemon Squeezy'nin 28 Ocak 2026 tarihli duyurusuna göre şirket Stripe Managed Payments'a taşınıyor. Managed Payments şu an "35+" ülkedeki satıcıları destekliyor. Desteklenmeyen ülkeler için Atlas öneriliyor (doğrulandı: https://www.lemonsqueezy.com/blog/2026-update). Managed Payments TR'yi desteklemediği için (doğrulandı, §2.1) Türkiye'deki satıcı için Lemon Squeezy'nin geleceği belirsiz. **Değerlendirme:** Yeni bir entegrasyon için Lemon Squeezy önerilmez.

### 2.3 Türk ödeme kuruluşları (yurt içi müşteri ve TL abonelik için)

- **iyzico:** "Subscription" ürünü günlük, haftalık, aylık ve yıllık planları ve deneme süresini destekliyor. Ürün eklenti olarak satın alınıyor: "ilk 3 ay ücretsiz, sonrasında 199 TL" (doğrulandı: https://docs.iyzico.com/en/products/subscription). Başarısız tahsilat için manuel "Retry Subscription Payment" servisi ve başarısız ödeme webhook'u var. `currencyCode` TRY, USD ve EUR değerlerini alıyor (doğrulandı: https://docs.iyzico.com/en/products/subscription/subscription-implementation/subscription-transactions). Otomatik yeniden deneme yapıp yapmadığı belgelenmemiş (doğrulanamadı).
- **PayTR:** Kayıtlı kartla tekrarlayan ödeme yapılıyor (`recurring_payment=1`, `non_3d=1`, `utoken`/`ctoken`). Önkoşullar: mağazada Non3D yetkisi olmalı ve tekrarlayan ödeme yetkisi PayTR tarafından onaylanmalı. Plan ve abonelik yönetimi yok, tahsilatı satıcı kendisi tetikliyor (doğrulandı: https://dev.paytr.com/en/direkt-api/kart-saklama-api/kayitli-kart-tekrarlayan-odeme).
- **Param:** SSS sayfasında "belirli tarih aralığında tekrarlı ödeme" alınabildiği yazıyor. Bu bilgi yalnızca arama özetinde görüldü. API ayrıntısı bulunamadı (doğrulanamadı: https://cert.param.com.tr/en/faq-business).

**Değerlendirme:** Türk ödeme kuruluşları MoR değildir. Yurt dışı KDV/VAT yükümlülüklerini üstlenmezler. Uluslararası satış için Paddle (veya FastSpring), yurt içi kurumsal TL faturalı satış için iyzico Subscription birlikte kullanılabilir.

### 2.4 Stripe webhook en iyi uygulamaları (Paddle ve iyzico için de aynı ilkeler geçerli)

Tümü şu kaynakta doğrulandı: https://docs.stripe.com/webhooks.
- **İmza doğrulama:** `Stripe-Signature` başlığı, **ham** gövde ve `whsec_` sırrı ile resmi kütüphane kullanılarak yapılır (.NET'te `EventUtility.ConstructEvent`). Gövdeyi değiştiren framework ayrıştırması doğrulamayı bozar. Manuel doğrulamada yalnızca `t` ve `v1` girdileri kullanılır. HMAC-SHA256 sabit zamanlı karşılaştırılır. Kütüphanelerin varsayılan toleransı **5 dakika**dır. Tolerans 0 yapılmamalıdır.
- **Yinelenen olaylar:** İşlenmiş event ID'leri kaydedilmeli ve tekrar işlenmemelidir. Ayrı Event nesneleri üretilebildiği için `data.object` ID'si ile `event.type` de birlikte kontrol edilmelidir.
- **Sıra garantisi yok:** Olay sırası için `created` kullanılmamalıdır. Eksik nesneler API'den çekilmelidir.
- **Yeniden denemeler:** Canlı modda **3 güne kadar** üstel geri çekilmeyle yeniden denenir. Sandbox'ta birkaç saat içinde 3 kez denenir. Dashboard'dan 15 gün, CLI'dan 30 gün içinde manuel yeniden gönderim yapılabilir.
- **Hızlı 2xx ve asenkron işleme:** Önce 2xx dönülür, karmaşık mantık sonra çalışır. İşleme asenkron kuyrukla yapılır, çünkü ay başı yenileme dalgaları yük oluşturur.
- **Diğer önlemler:** Yalnızca gereken olay türlerine abone olunur. CSRF muafiyeti verilir. HTTPS (TLS 1.2/1.3) kullanılır. Stripe IP allowlist uygulanır. Sır periyodik olarak döndürülür (eski sır 7 güne kadar geçerli kalabilir).
- **Giden istekler için idempotency:** Tüm v1 `POST` isteklerinde `Idempotency-Key` (V4 UUID önerilir, ≤255 karakter) kullanılır. Anahtarlar 24 saatten sonra temizlenebilir (doğrulandı: https://docs.stripe.com/api/idempotent_requests).

---

## 3. Windows kod imzalama (2026)

### 3.1 Microsoft Artifact Signing (eski adıyla Trusted Signing)

- **Fiyat:** Basic **9,99 $/ay** (5.000 imza/ay, her profil türünden 1 adet), Premium **99,99 $/ay** (100.000 imza/ay, her türden 10 adet). Kota aşımı **0,005 $/imza** (doğrulandı: Azure Retail Prices API, https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20'Trusted%20Signing'). Resmi fiyat sayfası kotaları gösteriyor ama fiyatları dinamik yüklüyor (doğrulandı: https://azure.microsoft.com/en-us/pricing/details/artifact-signing/). "Starts at $9.99/month" ifadesi Learn sayfasında da geçiyor (doğrulandı: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).
- **Uygunluk:** Learn'deki ifade şöyle: "Public Trust certificates are available to organizations in the United States, Canada, the European Union, the United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway, and Israel. Individual developers must be located in the United States or Canada." (doğrulandı: https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart, 2026-10-08 güncellemesi). Daha eski "code signing options" sayfası yalnızca ABD, Kanada, AB ve Birleşik Krallık'ı sayıyor (doğrulandı: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options). **Türkiye iki listede de yok.** Türkiye AB üyesi olmadığı için AB kapsamına da girmez.
- **Kimlik doğrulama:**
  - **Kuruluş:** Yasal ad, kuruluşa ait alan adında bir e-posta (7 gün geçerli link), işletme tanımlayıcısı ve adres istenir. Belgeler son 12 ayda düzenlenmiş olmalıdır. Belge yükleme için 3 hak var. Süreç 1-20 iş günü sürer.
  - **Bireysel:** Microsoft Entra Verified ID ve AU10TIX ile resmi kimlik ve yüz kontrolü yapılır. Azure faturalama hesabı "Individual" türünde olmalıdır.
  - **Genel koşullar:** Ücretsiz, deneme veya sponsorlu abonelikler desteklenmiyor (doğrulandı: quickstart ve https://learn.microsoft.com/en-us/azure/artifact-signing/faq).
- **Diğer özellikler:** EV sertifikası verilmez. Özel CN/O tanımlanamaz, CN yasal adla aynı olmalıdır. HSM'ler FIPS 140-3 Level 3 seviyesindedir (doğrulandı: FAQ).
- **Vergi geçmişi:** "3 yıllık vergi geçmişi" koşulu yalnızca Microsoft Q&A'daki yapay zeka üretimi yanıtlarda geçiyor, resmi belgede bulunamadı (doğrulanamadı).
- **ABD şirketi yolu:** Atlas ile kurulan bir ABD LLC'sinin "ABD kuruluşu" olarak doğrulanıp doğrulanamayacağı teyit edilemedi (doğrulanamadı).

### 3.2 Alternatifler: OV/EV ve bulut veya donanım anahtar saklama

- **CA/B Forum kuralı:** "Effective June 1, 2023, for Code Signing Certificates, CAs SHALL ensure that the Subscriber's Private Key is generated, stored, and used in a suitable Hardware Crypto Module". Asgari düzey FIPS 140-2 Level 2 veya Common Criteria EAL 4+ (doğrulandı: https://cabforum.org/working-groups/code-signing/requirements/).
- **Geçerlilik süresi:** 1 Mart 2026 ve sonrasında düzenlenen kod imzalama sertifikalarının geçerliliği **460 günü** aşamaz (doğrulandı: aynı URL). Bu, çok yıllık paketlerde yeniden düzenleme gerektiği anlamına geliyor.
- **Microsoft'un genel fiyat bandı:** OV için "$150–300/year", EV için "$400+/year" (doğrulandı: code-signing-options).

| CA / ürün | Anahtar saklama | Resmi sayfadaki fiyat |
|---|---|---|
| **Certum** Standard Code Signing | SimplySign bulut | **209 €**'dan başlıyor. Kart+okuyucu seti 169 €, EV bulut 379 € (doğrulandı: https://shop.certum.eu/code-signing.html). 27.02.2026'dan itibaren en fazla 459 gün geçerli (doğrulandı: aynı URL) |
| **SSL.com** OV | eSigner bulut veya token | OV **129 $/yıl** (2 yıl: 116,10 $/yıl, 3 yıl: 109,65 $/yıl) (doğrulandı: https://www.ssl.com/certificates/code-signing/buy/). eSigner OV Tier 1: **20 $/ay, 20 imza** (doğrulandı: https://www.ssl.com/guide/esigner-pricing-for-code-signing/) |
| **DigiCert** | KeyLocker bulut, token, kendi HSM'iniz | "Starting at $44/month/certificate" (12 ay toplamı 696 $). Diğer seçenek 62 $/ay (972 $). Hangisinin OV, hangisinin EV olduğu sayfada etiketlenmemiş (doğrulandı: https://www.digicert.com/signing/code-signing-certificates). KeyLocker'ın ayrıca ücretli olup olmadığı belli değil (doğrulanamadı) |
| **Sectigo** | Token, Google Cloud KMS, kendi HSM'iniz | "starts at $536.25 per year" (5 yıllık seçenekte). OV/EV ayrımı yok (doğrulandı: https://www.sectigo.com/ssl-certificates-tls/code-signing) |

### 3.3 SmartScreen (2024 sonrası)

- **EV artık atlatmıyor:** "EV certificates no longer bypass SmartScreen... Paying a premium for EV solely to avoid SmartScreen warnings is no longer justified." (doğrulandı: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation). Code signing options sayfasında "That behavior was removed in 2024" ifadesi geçiyor (doğrulandı: code-signing-options).
- **İki sinyal:** SmartScreen hem yayıncı itibarına hem de dosya hash itibarına bakar. Kesin bir eşik yok: "several weeks and hundreds of clean installs". Aynı imza kimliğiyle imzalanan sürümler yayıncı itibarını devralabilir (doğrulandı: smartscreen-reputation).
- **Smart App Control:** Windows 11'de Smart App Control, itibarı olmayan imzasız dosyaları engelleyebilir (doğrulandı: aynı URL).
- **Değerlendirme:** Türkiye için en iyi yol bulut HSM'li bir OV sertifikası (Certum SimplySign veya SSL.com eSigner) ve her sürümün aynı kimlikle imzalanmasıdır. EV'nin SmartScreen açısından getirisi yok.

---

## 4. Kurulum ve otomatik güncelleme

### 4.1 Velopack

- **Lisans ve sürüm:** MIT ("Copyright © 2024 Velopack Ltd.") (doğrulandı: https://github.com/velopack/velopack/blob/develop/LICENSE). NuGet'teki son kararlı sürüm 1.2.161, 1.3 ise ön sürüm (doğrulandı: https://www.nuget.org/packages/Velopack).
- **Delta güncellemeler:** Dosya bazında Zstandard ikili yamalardır. Birkaç sürüm geride kalan istemci ara deltaları sırayla uygular. Velopack, N delta ile tam paket arasında boyut sezgisiyle seçim yapar (doğrulandı: https://docs.velopack.io/packaging/deltas).
- **İmzalama:** Üç yol var: `--signParams` (signtool), `--signTemplate` (ör. AzureSignTool veya bir CA'nın bulut aracı, `{{file}}` yer tutucusuyla) ve `--azureTrustedSignFile` (Artifact Signing). Velopack uygulama ikililerini, Setup.exe'yi ve oluşturulmuşsa MSI'yı imzalar (doğrulandı: https://docs.velopack.io/packaging/signing).
- **Geri alma:** Ayrı bir rollback API'si yok. `UpdateOptions.AllowVersionDowngrade = true` ayarıyla daha düşük bir sürüme "güncellenebilir" (doğrulandı: https://docs.velopack.io/reference/cs/Velopack/UpdateOptions/properties/AllowVersionDowngrade).
- **Barındırma:** Herhangi bir statik dosya sunucusu veya GitHub Releases yeterli (doğrulandı: https://docs.velopack.io/).

### 4.2 WiX Toolset

- **Kaynak kod lisansı:** MS-RL (Microsoft Reciprocal License) (doğrulandı: https://github.com/wixtoolset/wix/blob/main/LICENSE.TXT).
- **OSMF:** İkili sürümler için OSMF EULA geçerli. Ücret, yıllık brüt geliri **≥ 10.000 USD** olan ve WiX'i gelir getiren işlerde kullanan kuruluşlar için zorunlu. Kaynaktan kendiniz derlerseniz bu anlaşma geçerli değil ("does not restrict the User from... self-compiling them") (doğrulandı: https://github.com/wixtoolset/wix/blob/main/OSMFEULA.txt).
- **Tutarlar:** GitHub Sponsors üzerinden kuruluş başına <20 çalışan için 10 $/ay, 20-100 çalışan için 40 $/ay, >100 çalışan için 60 $/ay (doğrulandı: https://github.com/sponsors/wixtoolset).
- **Sürümler:** Ücret ilk olarak v6'da getirildi. v7 EULA kabulünü zorunlu kıldı (`<AcceptEula>wix7</AcceptEula>` veya `wix eula accept wix7`) (doğrulandı: https://docs.firegiant.com/wix/osmf/). Güncel NuGet sürümü WixToolset.Sdk 7.0.1. nuspec'teki lisans dosyası OSMFEULA.txt (doğrulandı: https://www.nuget.org/packages/WixToolset.Sdk).
- **v5:** OSMF öncesi sürümdür. Son sürümü 5.0.2 (doğrulandı: NuGet sürüm listesi). Bakım durumu doğrulanamadı.

### 4.3 Inno Setup

- **Lisans:** "Permission is granted to anyone to use this software for any purpose, including commercial applications". Koşul olarak telif bildirimleri korunmalıdır (doğrulandı: https://jrsoftware.org/files/is/license.txt).
- **Ticari lisans:** Talep ediliyor ama zorunlu değil: "It is not strictly required... we request that all commercial users... purchase licenses". "Ticari kullanıcı" yıllık geliri 5.000 USD'yi aşan kâr amaçlı kuruluş olarak tanımlanıyor. Single, Team ve Enterprise katmanları var. Fiyatlar sayfada görünmüyor (doğrulandı: https://jrsoftware.org/isorder.php).

### 4.4 MSIX ve App Installer (kendi sitenizden dağıtım)

**Artılar:**
- **Otomatik güncelleme ve onarım:** `.appinstaller` dosyasıyla yapılır. Desteklenen ayarlar `HoursBetweenUpdateChecks`, `ShowPrompt`, `UpdateBlocksActivation` ve yedek `UpdateURI`. Windows 10 2004 ve üzeri ile Windows 11'de çalışır. `ShowPrompt` ve `UpdateBlocksActivation` için 2021 şeması gerekir (doğrulandı: https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview).
- **Temiz kaldırma:** Kaldırmanın temiz olması ayrıca doğrulanmadı (doğrulanamadı).

**Eksiler:**
- **Tek tık web kurulumu kapalı:** `ms-appinstaller:` protokolü Aralık 2023'ten beri tüketici cihazlarında varsayılan olarak kapalı. Microsoft, kullanıcının `.appinstaller` dosyasını indirip çift tıklamasını öneriyor (doğrulandı: https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web).
- **Güvenilir sertifika şart:** Paket güvenilir bir CA sertifikasıyla imzalanmalıdır. Self-signed sertifika her cihazın Trusted People deposuna yüklenmelidir (doğrulandı: aynı URL).
- **Değerlendirme:** Konteyner ve sanallaştırma kısıtlamaları SQLite veri yolu ve COM gibi konularda test gerektirir.

**Değerlendirme:** Kendi sitesinden dağıtılan bir WPF uygulaması için Velopack (MIT, delta, imzalama entegrasyonu) en az sürtünmeli seçenek. Kurumsal müşteri MSI isterse WiX kullanılabilir (OSMF bütçelenmeli). Microsoft Store ayrı bir kanal olarak değerlendirilebilir, çünkü Store'daki MSIX paketleri ücretsiz imzalanıyor ve SmartScreen uyarısı çıkmıyor (doğrulandı: code-signing-options).

---

## 5. Platform ve kütüphane doğrulaması

### 5.1 .NET destek tarihleri

Tüm tarihler şu kaynakta doğrulandı: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core.

| Sürüm | Tür | Destek sonu | Son yama |
|---|---|---|---|
| .NET 8 | LTS | **10 Kasım 2026 (bitti)** | 8.0.34 |
| .NET 9 | STS | **10 Kasım 2026 (bitti)** | 9.0.23 |
| .NET 10 | LTS | **14 Kasım 2028** | 10.0.15 |
| .NET 11 | STS (10 Kasım 2026'da yayımlandı) | 14 Kasım 2028 | 11.0.0 |

**Önemli:** `Microsoft.EntityFrameworkCore.Sqlite` 11.0.0 yalnızca `net11.0` hedefliyor. .NET 10 LTS üzerinde **EF Core 10.0.x** (şu an 10.0.15) kullanılmalıdır (doğrulandı: NuGet nuspec, https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite).

### 5.2 Kütüphane lisansları

| Paket | Son kararlı sürüm | Lisans | Kaynak |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT | doğrulandı: https://www.nuget.org/packages/CommunityToolkit.Mvvm |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.15 / 11.0.0 | MIT | doğrulandı: https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite |
| SQLitePCLRaw.core / bundle_e_sqlite3 | 3.0.5 (EF Core 10, 2.1.13'e bağlı) | Apache-2.0 | doğrulandı: https://www.nuget.org/packages/SQLitePCLRaw.core |
| Microsoft.Extensions.Hosting | 10.0.15 / 11.0.0 | MIT | doğrulandı: https://www.nuget.org/packages/Microsoft.Extensions.Hosting |
| Serilog | 4.4.0 | Apache-2.0 | doğrulandı: https://www.nuget.org/packages/Serilog |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | doğrulandı: https://www.nuget.org/packages/Serilog.Sinks.File |
| xunit.v3 | 4.0.3 | Apache-2.0 | doğrulandı: https://www.nuget.org/packages/xunit.v3 |
| ClosedXML | 0.105.1 | MIT | doğrulandı: https://www.nuget.org/packages/ClosedXML |
| QuestPDF | 2026.9.1 | Özel (Community/Professional/Enterprise) | doğrulandı: https://www.questpdf.com/pricing.html |

**SQLCipher:**
- **Ücretsiz paket dondu:** SQLitePCLRaw bakımcısının ifadesi: "I no longer publish encryption-enabled SQLite builds without cost." SQLCipher derlemelerinin Zetetic'ten satın alınması öneriliyor. Ücretli alternatifler SEE ve SourceGear derleme hizmeti (doğrulandı: https://github.com/ericsink/SQLitePCL.raw/blob/main/README.md). Ücretsiz `SQLitePCLRaw.bundle_e_sqlcipher` paketi 2.1.11'de kaldı. 3.x sürümü yok (doğrulandı: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher).
- **Ücretsiz alternatif:** SQLite3 Multiple Ciphers. NuGet paketi `SQLite3MC.PCLRaw.bundle` 2.6.1, MIT lisanslı, Ulrich Telle, Ekim 2026 (doğrulandı: https://www.nuget.org/packages/SQLite3MC.PCLRaw.bundle).
- **Uyumluluk notu:** SQLite3MC provider'ı `SQLitePCLRaw.core` 3.0.2'ye bağlı, EF Core 10 ise 2.1.13 kullanıyor. Bu kombinasyonun sorunsuz çalıştığı doğrulanamadı (doğrulanamadı). Prototipte test edilmeli.

**QuestPDF:**
- **Community (ücretsiz):** Son mali yılda yıllık brüt geliri **1.000.000 USD'nin altında** olan kuruluşlar, bireyler, açık kaynak projeler ve uygun hayır ve akademik kurumlar için. Halka açık şirketler ve kamu kurumları gelirden bağımsız olarak hariç. Eşik aşılırsa 90 günlük geçiş süresi var (doğrulandı: https://www.questpdf.com/license/guide.html).
- **Ücretli:** Professional **1.999 $** ve Enterprise **4.999 $**, yıllık yenilemeli, sınırsız geliştirici (doğrulandı: https://www.questpdf.com/pricing.html).

---

## 6. Önerilen yol haritası (Değerlendirme)

1. **Ödeme:** Uluslararası satışta MoR olarak Paddle (TR satıcıyı kabul ediyor, KDV/VAT'ı üstleniyor). Yurt içi TL kurumsal satışta iyzico Subscription. Stripe ancak Atlas ile ABD şirketi kurulursa mümkün. Bu yol Form 5472 ve Delaware yükümlülükleri getiriyor ve MoR değil.
2. **Lisans:** Paddle webhook'larını tüketen küçük bir ASP.NET Core API (.NET 10 LTS) ve ECDSA P-256 imzalı offline lisans belgeleri (BCL, sıfır bağımlılık). Dış kaynak kullanmak isterseniz Keygen Cloud Std 1 (990 $/yıl, 1.000 ALU).
3. **İmza:** Bulut HSM'li OV sertifikası (Certum SimplySign 209 €'dan veya SSL.com OV 129 $/yıl + eSigner 20 $/ay). Velopack'e `--signTemplate` ile bağlanır. EV almaya gerek yok.
4. **Kurulum:** Velopack. MSI gerekirse WiX v7 ve OSMF 10 $/ay.
5. **Platform:** .NET 10 LTS, EF Core 10.0.x. Şifreli SQLite gerekiyorsa SQLite3MC ile uyumluluk testi veya ticari SQLCipher. QuestPDF Community, gelir 1M USD'nin altında kaldıkça yeterli.

---

## Kaynaklar

**Lisanslama**
- https://github.com/keygen-sh/keygen-api/blob/master/LICENSE.md
- https://github.com/keygen-sh/keygen-api/blob/master/README.md
- https://github.com/keygen-sh/keygen-api/blob/master/Gemfile
- https://keygen.sh/docs/self-hosting/
- https://keygen.sh/pricing/ ; https://api.keygen.sh/v1/plans?limit=100
- https://keygen.sh/docs/api/cryptography/
- https://keygen.sh/docs/api/machines/
- https://keygen.sh/docs/api/entitlements/
- https://github.com/keygen-sh/example-stripe-integration
- https://github.com/keygen-sh/example-csharp-cryptographic-license-files
- https://devolens.com/pricing
- https://www.nuget.org/packages/Cryptolens.Licensing ; https://github.com/Cryptolens/cryptolens-dotnet/
- https://help.cryptolens.io/examples/offline-verification
- https://help.cryptolens.io/licensing-models/floating
- https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries
- https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries
- https://github.com/dotnet/runtime/issues/14741
- https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.ecdsa?view=net-10.0
- https://www.nuget.org/packages/NSec.Cryptography ; https://www.nuget.org/packages/libsodium ; https://www.nuget.org/packages/BouncyCastle.Cryptography
- https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit

**Ödemeler**
- https://stripe.com/global
- https://stripe.com/atlas ; https://docs.stripe.com/atlas ; https://docs.stripe.com/atlas/signup
- https://docs.stripe.com/payments/managed-payments ; https://docs.stripe.com/payments/managed-payments/eligibility
- https://www.irs.gov/instructions/i5472
- https://www.paddle.com/help/start/account-verification/which-countries-are-supported-by-paddle ; https://www.paddle.com/pricing ; https://www.paddle.com/blog/payments-in-turkey
- https://docs.lemonsqueezy.com/help/getting-started/supported-countries ; https://www.lemonsqueezy.com/pricing ; https://www.lemonsqueezy.com/blog/2026-update
- https://fastspring.com/terms-use/restricted-countries ; https://fastspring.com/pricing/
- https://docs.iyzico.com/en/products/subscription ; https://docs.iyzico.com/en/products/subscription/subscription-implementation/subscription-transactions
- https://dev.paytr.com/en/direkt-api/kart-saklama-api/kayitli-kart-tekrarlayan-odeme
- https://cert.param.com.tr/en/faq-business (yalnızca arama özeti)
- https://docs.stripe.com/webhooks ; https://docs.stripe.com/api/idempotent_requests

**Kod imzalama**
- https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- https://learn.microsoft.com/en-us/azure/artifact-signing/faq
- https://azure.microsoft.com/en-us/pricing/details/artifact-signing/ ; https://prices.azure.com/api/retail/prices (serviceName 'Trusted Signing')
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
- https://cabforum.org/working-groups/code-signing/requirements/
- https://shop.certum.eu/code-signing.html
- https://www.ssl.com/certificates/code-signing/buy/ ; https://www.ssl.com/guide/esigner-pricing-for-code-signing/
- https://www.digicert.com/signing/code-signing-certificates
- https://www.sectigo.com/ssl-certificates-tls/code-signing

**Kurulum ve güncelleme**
- https://github.com/velopack/velopack/blob/develop/LICENSE ; https://www.nuget.org/packages/Velopack
- https://docs.velopack.io/ ; https://docs.velopack.io/packaging/deltas ; https://docs.velopack.io/packaging/signing ; https://docs.velopack.io/reference/cs/Velopack/UpdateOptions/properties/AllowVersionDowngrade
- https://github.com/wixtoolset/wix/blob/main/LICENSE.TXT ; https://github.com/wixtoolset/wix/blob/main/OSMFEULA.txt ; https://github.com/sponsors/wixtoolset ; https://docs.firegiant.com/wix/osmf/
- https://jrsoftware.org/files/is/license.txt ; https://jrsoftware.org/isorder.php
- https://learn.microsoft.com/en-us/windows/msix/app-installer/installing-windows10-apps-web
- https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview

**Platform ve kütüphaneler**
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://github.com/ericsink/SQLitePCL.raw/blob/main/README.md
- https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher ; https://www.nuget.org/packages/SQLite3MC.PCLRaw.bundle
- https://www.questpdf.com/license/guide.html ; https://www.questpdf.com/pricing.html
- NuGet sayfaları: CommunityToolkit.Mvvm, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.Hosting, Serilog, Serilog.Sinks.File, xunit.v3, ClosedXML, WixToolset.Sdk
