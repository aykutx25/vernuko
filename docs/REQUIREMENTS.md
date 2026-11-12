# Gereksinimler

Kaynak: proje talimatı (Vernuko ana geliştirme talimatı). Bu belge talimatı izlenebilir gereksinimlere çevirir ve her birinin durumunu gösterir. Durum: ✅ uygulandı + test · 🟡 uygulandı, Windows'ta doğrulanacak · ⬜ planlı (faz).

## 1. Ürün ilkeleri
| Kod | Gereksinim | Durum |
|---|---|---|
| U-1 | Tek randevu motoru; meslekler modül/profil olarak eklenir | ✅ |
| U-2 | İnternetsiz temel işlemler (randevu, müşteri, seans, kasa) | ✅ |
| U-3 | Müşteri verisi varsayılan olarak işletmenin bilgisayarında | ✅ |
| U-4 | Bulut yedek isteğe bağlı ve müşterinin hesabında | ⬜ Faz 4 (yerel ✅) |
| U-5 | SMS/WhatsApp masrafı kullanıcı onayı olmadan oluşmaz | ✅ (ücretli entegrasyon yok; manuel WhatsApp ücretsiz) |
| U-6 | Lisans yokluğu/erişilemezliği verileri yok etmez | ✅ |
| U-7 | Sade, özgün, profesyonel tasarım; hızlı randevu | 🟡 |
| U-8 | Güzellik ve lazer tek sektör | ✅ |

## 2. Fonksiyonel gereksinimler
### Kurulum
| Kod | Gereksinim | Durum |
|---|---|---|
| K-1 | 10 adımlı sihirbaz (hoş geldiniz → tamamla) | ✅ VM / 🟡 ekran |
| K-2 | Zorunlu olmayan adımlar atlanabilir (personel, hizmet, yedek) | ✅ |
| K-3 | Demo verisi ayrı demo modunda (`--demo`, ayrı klasör ve DB) | ✅ |

### Randevu motoru
| Kod | Gereksinim | Durum |
|---|---|---|
| R-1 | Gün / hafta / ay / liste görünümleri; personel, oda, hizmet filtresi | ✅ VM / 🟡 ekran |
| R-2 | Müsait saat hesaplama, hizmete göre süre, hazırlık/temizlik | ✅ |
| R-3 | Personel/oda/cihaz çakışma kontrolü (uygulama + eşzamanlı + DB) | ✅ |
| R-4 | 10 durum ve tutarlı geçiş kuralları; tamamlanmamış randevu seans tüketmez | ✅ |
| R-5 | Erteleme/yeniden planlama, iptal (gerekçeli), iptali geri alma | ✅ |
| R-6 | Personel izni, resmî tatil, kapalı gün | ✅ |
| R-7 | Geç gelen / gelmeyen; gelmeme politikası (seans düşülsün mü) | ✅ |
| R-8 | Randevu notları, geçmiş, durum değişiklik geçmişi | ✅ |
| R-9 | Hızlı müşteri arama, klavye kısayolları | ✅ VM / 🟡 ekran |
| R-10 | Tekrarlayan randevu, bekleme listesi, randevu onayı akışı | Onay durumu ✅; tekrarlayan ve bekleme listesi ⬜ P1 |
| R-11 | Sürükle-bırak | ⬜ P1 |
| R-12 | Otomatik hatırlatma | ⬜ Faz 5 (manuel WhatsApp ✅) |

### Müşteri
| Kod | Gereksinim | Durum |
|---|---|---|
| M-1 | Temel alanlar, müşteri no, iletişim tercihleri, notlar | ✅ |
| M-2 | T.C. kimlik isteğe bağlı, sağlama kontrolü, yetkisiz kullanıcıya maskeli | ✅ |
| M-3 | Aynı telefonu kullanan aile üyeleri; mükerrer uyarısı, otomatik birleştirme yok | ✅ |
| M-4 | Arşiv, anonimleştirme, dışa aktarma | ✅ |
| M-5 | Pazarlama izni operasyonel bildirimlerden ayrı | ✅ |
| M-6 | İşletme tarafından yönetilen saklama politikası (otomatik imha) | ⬜ P1 |

### Kasa ve paketler
| Kod | Gereksinim | Durum |
|---|---|---|
| F-1 | Hizmet fiyatı, paket satışı, peşin/kısmi ödeme, kapora, kalan bakiye | ✅ |
| F-2 | Nakit/kart/havale tahsilat kaydı, iade, indirim | ✅ |
| F-3 | Günlük kasa, borçlu listesi, işlem geçmişi | ✅ |
| F-4 | Sessiz silme yok; düzeltme ters kayıtla | ✅ (DB tetikleyicisi) |
| F-5 | Personel primi | ⬜ P1 |
| F-6 | Resmî fatura/e-Arşiv düzenlenmez | ✅ (bilinçli) |
| P-1 | Seans paketi: toplam/kullanılan/kalan, bölge, geçerlilik, dondurma, uzatma, düzeltme, geçmiş | ✅ |

### Yedekleme, iletişim, lisans
Ayrıntılar: BACKUP_RECOVERY.md, INTEGRATIONS.md, LICENSING.md.

### Yetki
| Kod | Gereksinim | Durum |
|---|---|---|
| Y-1 | Roller: sahip, yönetici, sekreter, uzman, personel, muhasebe, özel | ✅ |
| Y-2 | İşlem bazlı izinler (28), rol varsayılanı + ek/çıkar | ✅ (ek/çıkar arayüzü ⬜ P1) |
| Y-3 | Önemli işlemler denetim günlüğünde | ✅ |
| Y-4 | Güçlü parola karması, oturum kilidi | ✅ |

## 3. Fonksiyonel olmayan gereksinimler
| Kod | Gereksinim | Hedef | Durum |
|---|---|---|---|
| N-1 | Hızlı açılış | Soğuk açılış < 3 sn (orta seviye PC) | ⬜ ölçülmedi |
| N-2 | Düşük boşta RAM | < 200 MB | ⬜ ölçülmedi |
| N-3 | Akıcı arama | 20 bin müşteride < 1,5 sn (CI eşiği); hedef < 300 ms | ✅ eşik testi |
| N-4 | Donmayan arayüz | Tüm veri erişimi async; uzun işlemler meşgul göstergeli | ✅ tasarım / 🟡 ölçüm |
| N-5 | Ekranlar | 1366×768 ve 1920×1080; yüksek DPI | 🟡 smoke test |
| N-6 | Türkçe karakterler | Arama, isim büyük/küçük harf, CSV (BOM) | ✅ |
| N-7 | Saat dilimi / yaz saati | UTC saklama, DST günleri | ✅ |
| N-8 | Veri dayanıklılığı | Elektrik kesintisinde işlenmiş kayıt kaybolmaz (WAL + synchronous=FULL) | ✅ yapılandırma |

Performans rakamları gerçek cihazlarda ölçülmeden başarı olarak ilan edilmez (Faz 8).

## I. Başarı kriterleri (ilk çalışan sürüm)
| # | Kriter | Otomatik doğrulama | Windows'ta elle |
|---|---|---|---|
| 1 | Kullanıcı Vernuko'yu kurar | — (yükleyici Faz 8) | Şimdilik: `dotnet run` / publish klasörü |
| 2 | "Güzellik ve Lazer Merkezi" profilini seçer | `SuccessCriteriaTests`, `Setup_wizard_walks_through_all_steps_and_signs_in` | 🟡 |
| 3 | İşletmesini ve personelini tanımlar | aynı | 🟡 |
| 4 | Müşteri ekler | aynı + `Front_desk_books_with_quick_customer_and_suggested_slot` | 🟡 |
| 5 | Hizmet ve randevu oluşturur | aynı | 🟡 |
| 6 | Takvimde randevuyu görüntüler | aynı (gün listesi + seans etiketi) | 🟡 |
| 7 | Randevuyu tamamlar | aynı | 🟡 |
| 8 | Seans paketi varsa kalan hak güncellenir (8 → 7) | aynı | 🟡 |
| 9 | Veriler kapatılıp açıldığında korunur | aynı + `Data_survives_application_restart` | 🟡 |
| 10 | Yerel şifreli yedek oluşturulur | aynı + `Encrypted_backup_restores_previous_state` | 🟡 |
| 11 | Yedekten geri yükleme başarıyla test edilir (yeni bilgisayar + kurtarma anahtarı dahil) | aynı + `Restore_on_a_new_computer_needs_the_recovery_key` | 🟡 |

`tests/Vernuko.Tests/Integration/SuccessCriteriaTests.cs` 2–11 numaralı adımları gerçek SQLite dosyası üzerinde, arayüzün kullandığı aynı servislerle uçtan uca çalıştırır ve geçer. Arayüz üzerinden elle doğrulama Windows'ta yapılmalıdır; bu yapılmadan Faz 3'e geçilmemelidir.
