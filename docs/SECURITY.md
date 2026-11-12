# Güvenlik ve Gizlilik (KVKK)

> Bu belge teknik önlemleri açıklar. **Vernuko'nun kendiliğinden "KVKK uyumlu" olduğu iddia edilmez**; uyum, işletmenin süreçlerine ve hukuki değerlendirmeye de bağlıdır. ⚖️ işaretli konular avukat görüşü gerektirir.

## Tehdit modeli (Model A)
| Varlık | Tehdit | Önlem |
|---|---|---|
| Müşteri verisi (yerel DB) | Ön büro bilgisayarına yetkisiz kişi erişimi | Kullanıcı girişi, otomatik ekran kilidi (varsayılan 10 dk), rol/işlem yetkisi, hesap kilitleme |
| | Disk/bilgisayar çalınması | ⚠️ Faz 1'de veritabanı dosyası şifrelenmiyor. **BitLocker önerilir** (kurulum belgesinde). Seçenekler: SQLite3 Multiple Ciphers (MIT, EF Core 10 uyumluluğu test edilmeli) veya ticari SQLCipher — P1 kararı. Özel nitelikli alanlar için alan bazlı şifreleme P1. |
| Yedekler | Bulutta/USB'de okunması | AES-256-GCM STREAM, kurtarma anahtarı, başlıkta kişisel veri yok |
| Finansal kayıtlar | Sessiz silme/değiştirme | Append-only tetikleyiciler, ters kayıt |
| Denetim kaydı | Geçmişin değiştirilmesi | Append-only + hash zinciri; doğrulama ekranı. (Dosyaya tam erişimi olan saldırgan zinciri yeniden hesaplayabilir; bu kurcalamayı *tespit* eder, tam engellemez. Harici imzalı kontrol noktaları P2.) |
| Lisans | Taklit / süre uzatma | ECDSA imza, özel anahtar istemcide yok, saat geri alma koruması |
| Lisans sunucusu | Anahtar tahmini, sahte ödeme bildirimi, yönetici erişimi | 100 bit anahtar + IP başına dakikada 30 istek; Paddle HMAC imzası + olay kimliği tekrar koruması; yönetici anahtarının yalnızca SHA-256 özeti sunucuda, sabit zamanlı karşılaştırma; sırlar dosyada (600 izin), depoda değil; müşteri verisi sunucuya gitmez |
| Güncelleme | Sahte paket | Güncelleme kaynağı derleme anında sabitlenir, yalnızca HTTPS; çalışma anında değiştirilemez; Velopack paket SHA doğrulaması. Authenticode imzalı yükleyici OV sertifika alınınca (bkz. RELEASE.md) |
| Müşteri fotoğraf/belge dosyaları | Diskten okunması | Dosya başına AES-256-GCM; dosya anahtarı DPAPI ile ve yedek anahtarıyla sarılı; önizleme yalnızca bellekte; her görüntüleme denetim kaydında; şifresiz kopya yalnızca kullanıcının açık "dışa aktar" onayıyla |
| Prim/stok kayıtları | Sonradan değiştirilmesi | `CommissionEntries`, `StockMovements` append-only tetikleyicili; düzeltme ters kayıtla |

## Uygulanan kontroller
- **Kimlik doğrulama:** PBKDF2-HMAC-SHA256, 600.000 iterasyon (OWASP), 16 bayt tuz, sürümlü biçim (`v1$pbkdf2-sha256$…`) ve girişte otomatik yeniden karma. Sabit zamanlı karşılaştırma. Olmayan kullanıcı için de aynı maliyette doğrulama (kullanıcı adı yoklaması engeli).
- **Parola politikası:** en az 8 karakter, en az 4 farklı karakter, yaygın parola listesi, kullanıcı adını içermeme.
- **Hesap kilidi:** 5 hatalı denemeden sonra 1, 2, 4… dakika (en çok 64) kilit; başarılı girişte sıfırlanır; hatalı denemeler denetim kaydında.
- **Oturum kilidi:** boşta kalma süresi (ayarlanabilir), Ctrl+L ile elle kilit; kilit açma parolayla; kilitliyken tüm servis çağrıları reddedilir.
- **Yetki:** 34 işlem izni, 7 rol (Faz 2 ile: stok görüntüleme/yönetme, ürün satışı, onam kaydı, müşteri dosyası görüntüleme/yönetme); her servis metodu izin kontrolü yapar (arayüz gizlemesine güvenilmez). Sahip hesabını yalnızca sahip değiştirebilir; son aktif sahip devre dışı bırakılamaz.
- **Hassas alanlar:** T.C. kimlik no yetkisiz kullanıcıya maskeli (`*******0146`); yetkisiz kullanıcının kaydetmesi alanı silmez.
- **Yetkisiz erişim denemeleri** (finans raporları, kullanıcı yönetimi, geri yükleme, denetim, dışa aktarma, hassas veri) denetim kaydına yazılır.
- **SQL injection:** tüm sorgular EF Core parametreli; ham SQL yalnızca sabit metinler ve parametreli `VACUUM INTO`.
- **Dosya/arşiv güvenliği:** yedek arşivinde zip-slip kontrolü, dosya adı doğrulama, `.partial` + atomik taşıma; hedef klasörde yol geçişi engeli.
- **Sırlar:** kaynak kodda gizli anahtar yok; yedek KEK'i DPAPI (CurrentUser) ile korunur; lisans özel anahtarı istemcide yok. Linux geliştirme ortamında DPAPI yerine yalnızca geliştirme amaçlı dosya anahtarı kullanılır (Windows uygulamasında asla).
- **Günlükler:** müşteri adı, telefon, not, mesaj içeriği yazılmaz; yalnızca kimlik, hata türü ve sayılar. Denetim özetleri de kişisel veriyi asgari tutar (müşteri numarası, tarih).
- **CSV formül enjeksiyonu:** `=`, `+`, `-`, `@` ile başlayan hücreler etkisizleştirilir.
- **Ağ:** Uygulama kendiliğinden ağ çağrısı yapmaz. Tek istisna, kullanıcının Ayarlar › Güncellemeler'de "Güncellemeleri denetle" düğmesine basmasıdır (yalnızca derlemede bir HTTPS güncelleme kaynağı tanımlıysa). wa.me bağlantısı kullanıcının varsayılan tarayıcısında açılır; yalnızca http/https şeması kabul edilir. Telemetri yok.
- **Bağımlılıklar:** merkezi sürüm sabitleme, NuGet güvenlik denetimi derlemede açık.
- **Windows:** uygulama yönetici yetkisi istemez (`asInvoker`). Windows Defender/SmartScreen ayarlarını değiştirmeye çalışmaz; imzalama ve itibar yoluyla güven kazanılır (Faz 8: OV sertifika + bulut HSM; imza SmartScreen uyarısının çıkmayacağını garanti etmez).

## KVKK
| Konu | Teknik karşılık | Durum |
|---|---|---|
| Veri minimizasyonu | T.C. kimlik, doğum tarihi, adres, e-posta isteğe bağlı; mesajlarda asgari bilgi | ✅ |
| Yetki ayrımı | Rol/işlem izinleri; hassas alan maskesi; gizli notlar (P1) | ✅ / P1 |
| Saklama ve imha | Arşiv, anonimleştirme (geri alınamaz, gerekçeli, denetimli); işletme tanımlı otomatik saklama süresi | ✅ / P1 |
| İlgili kişi hakları (md. 11) | Müşteri bazlı tam JSON dışa aktarım; anonimleştirme | ✅ |
| İşlem geçmişi | Hash zincirli denetim kaydı | ✅ |
| Aydınlatma ve onay kayıtları | Pazarlama izni tarih + kaynakla; sürümlü onam/bilgilendirme formları, müşteri bazlı onay kaydı (yöntem, tarih, kaydeden; geri alma gerekçeli ve silinmez). Metinleri Vernuko sağlamaz ⚖️ | ✅ |
| Özel nitelikli veri (sağlık, fotoğraf, terapi notu) | Sağlık profillerinde mesajda hizmet adı gizli; fotoğraflar şifreli ve ayrı yetkili (sekreter varsayılan olarak göremez); anonimleştirmede tüm dosyalar silinir; gizli uzman notları P1 | ✅ / P1 |
| Yurt dışı aktarım | ⚖️ Google Drive/OneDrive/Dropbox'a yedek KVKK rehberinde yurt dışı aktarım sayılabilir; düzenli yedek "arızi" değildir; standart sözleşme + 5 iş günü içinde Kurul'a bildirim gerekebilir. Tüketici hesaplarıyla sözleşmenin mümkün olup olmadığı doğrulanamadı. | Bulut kopyası uygulandı: **varsayılan kapalı**, açmak için yurt dışı aktarım bilgilendirmesinin onaylanması zorunlu (zaman + denetim kaydı), yalnızca şifreli dosya gider. Avukat görüşü hâlâ şart |
| Pazarlama ile hizmet iletişimi ayrımı | Müşteri kartında ayrı izin; ticari ileti İYS kontrolüyle (Faz 5) | ✅ / Faz 5 |
| VERBİS | İşletmenin yükümlülüğü (sağlıkta yeni eşik: <10 çalışan ve <10 M₺ bilanço muafiyeti) | Bilgilendirme |
| Vernuko'nun rolü | Veriye erişmediği sürece (telemetri/uzaktan destek yok) veri işleyen olmaması hedeflenir ⚖️ | Faz 1'de telemetri yok |

Avukata sorulacak sorular: [research/turkey-market-and-law.md › C](research/turkey-market-and-law.md).

## Güvenlik açığı bildirimi
Güvenlik sorunlarını herkese açık issue yerine doğrudan proje sahibine bildirin. Yayınlanmış bir ticari sürüm olduğunda bu bölüme iletişim adresi ve yanıt süresi eklenecek.
