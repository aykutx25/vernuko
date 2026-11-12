# H. Yol Haritası ve Önceliklendirilmiş İş Listesi

**Öncelik tanımları** — P0: ticari ürünün temel kullanımı için zorunlu · P1: ilk ticari sürümü güçlendirir · P2: gelecekteki genişletmeler.
**Durum** — ✅ uygulandı ve otomatik testle doğrulandı · 🟡 uygulandı, Windows'ta çalıştırılarak doğrulanması gerekiyor · ⬜ yapılmadı.

## Faz özeti
| Faz | Kapsam | Durum |
|---|---|---|
| 0 | Araştırma, mimari, veri modeli, güvenlik modeli, lisans kararı, maliyet, UI sistemi, plan | ✅ (bu belgeler) |
| 1 | Windows çekirdeği: kurulum, SQLite, kullanıcılar, müşteri, personel, hizmet, randevu motoru, testler | ✅ çekirdek · 🟡 arayüz |
| 2 | Güzellik & Lazer: paket/seans, bölge, oda/cihaz, ödeme, iptal/iade, prim, stok, onam/fotoğraf, tekrarlayan randevu, bekleme listesi | ✅ iş kuralları ve view model'ler (testli) · 🟡 ekranlar Windows'ta doğrulanacak |
| 3 | Diğer meslekler (önce kuaför, SPA, danışmanlık) | ⬜ (profil kataloğu hazır) |
| 4 | Yedekleme: yerel ✅ · OneDrive/Google Drive/Dropbox eşitleme klasörüne şifreli kopya ✅ · API (OAuth) bağlantısı ⬜ (sahip uygulama kaydı açmalı) | ✅ temel |
| 5 | İletişim: SMS, WhatsApp API, e-posta, hatırlatma planlama | ✅ hatırlatma planlayıcı + giden kutusu + İleti Merkezi SMS + WhatsApp Cloud API (testli, gerçek hesapla denenmedi) · ⬜ Netgsm, Verimor, e-posta, ICS |
| 6 | Lisans sunucusu ve satış (Paddle/iyzico) | ⬜ (istemci doğrulama ✅) |
| 7 | Çoklu bilgisayar: yerel API, PostgreSQL, eşzamanlılık, veri taşıma | ⬜ |
| 8 | Üretime hazırlık: güvenlik denetimi, performans, kurulum, imzalama, güncelleme, dokümantasyon | Kısmen: kurulum paketi + güncelleme ✅ (Linux'ta paket üretildi, Windows'ta kur/güncelle denenmedi 🟡) · imzalama ⬜ (sertifika yok) |

**Kural:** Başarı kriterleri (REQUIREMENTS.md › I) Windows'ta uçtan uca doğrulanmadan Faz 3'e geçilmez.

## P0 — Zorunlu
| # | İş | Bağımlılık | Kabul kriteri | Durum |
|---|---|---|---|---|
| P0-1 | Modüler çözüm, .NET 10 LTS, merkezi paket yönetimi, analizörler hata seviyesinde | — | `dotnet build Vernuko.sln` uyarısız | ✅ |
| P0-2 | SQLite kalıcılık, WAL, IMMEDIATE işlemler, migration + güvenlik kopyası + sürüm koruması | P0-1 | Migration hatasında veri korunur; yeni sürüm DB reddedilir (test) | ✅ |
| P0-3 | Randevu motoru: 10 durum, geçiş kuralları, çakışma (personel/oda/cihaz + tampon), müsait saat, DST | P0-2 | Çakışma, izin, mesai, DST testleri geçer; eşzamanlı 8 istekten 1'i kaydedilir | ✅ |
| P0-4 | DB seviyesinde çakışma ve append-only tetikleyicileri | P0-2 | Uygulama atlatılsa bile çift kayıt/silme reddedilir (test) | ✅ |
| P0-5 | Müşteri kartı, Türkçe arama, mükerrer uyarısı, arşiv, anonimleştirme, JSON/CSV dışa aktarım | P0-2 | "isik" → "Işık"; 20 bin kayıtta arama < 1,5 sn (CI) | ✅ |
| P0-6 | Paket/seans defteri: satış, tamamlanınca düşme, iptalde korunma, geri alma, düzeltme, dondurma | P0-3 | 8→7; iptal hak düşmez; geri alma 8'e döner (test) | ✅ |
| P0-7 | Cari hesap: tahsilat, kapora, borç, ters kayıt, idempotent ödeme, günlük kasa | P0-2 | Çift tıklama tek kayıt; ters kayıt ikinci kez yapılamaz (test) | ✅ |
| P0-8 | Kullanıcılar, roller, işlem bazlı izin, PBKDF2, hesap kilitleme, otomatik ekran kilidi, denetim günlüğü (hash zinciri) | P0-2 | Sekreter finans raporu göremez ve deneme kaydedilir; zincir kurcalaması tespit edilir (test) | ✅ |
| P0-9 | Şifreli yerel yedek, doğrulama, geri yükleme, kurtarma anahtarı, saklama | P0-2 | Yeni bilgisayarda anahtarla geri yükleme; bozuk/yanlış anahtar reddi (test) | ✅ |
| P0-10 | Lisans istemcisi: imzalı belge, çevrimdışı tolerans, salt-okunur mod, saat geri alma | P0-2 | Süre bitince yazma engellenir, okuma/yedek/dışa aktarma çalışır (test) | ✅ |
| P0-11 | Kurulum sihirbazı (10 adım, atlanabilir adımlar), demo modu ayrı veritabanında | P0-3..9 | Sihirbaz VM testi; demo DB gerçek DB'den ayrı | ✅ VM · 🟡 ekran |
| P0-12 | WPF arayüz: kabuk, randevu gün/hafta/ay, yan panelde hızlı randevu, müşteri, hizmet, personel, paket, ödeme, rapor, ayarlar | P0-3..11 | Smoke test (Windows) 0 bağlama hatası; başarı kriterleri elle uçtan uca | ✅ smoke test + **başarı senaryosu gerçek WPF ekranlarında otomatik** (iki Windows bilgisayarda, 2026-11-12) · 🟡 kurulum sihirbazının elle tamamlanması ayrıca denenmeli |
| P0-13 | Windows CI: derleme + test + smoke test + ekran görüntüsü artefaktı | P0-12 | Workflow yeşil | ✅ yerelde `kontrol.cmd` ile (Windows'ta 141/141 + smoke test temiz, 2026-11-12) · GitHub Actions yalnızca elle |
| P0-14 | Kurulum paketi (Velopack), güncelleme öncesi yedek, imzalı yükleyici (OV + bulut HSM) | P0-12 | Temiz Windows 10/11'de kur-kaldır-güncelle | 🟡 paket + taslak sürüm iş akışı + güncelleme öncesi yedek (test) + Windows'ta paket üretildi ve kuruldu · ⏸ **imza: sahip kararıyla ertelendi (2026-11-12)** — kabul edilen risk: Akıllı Uygulama Denetimi açık Windows 11 bilgisayarlarda program açılmaz, diğerlerinde SmartScreen uyarısı çıkar; gerektiğinde kod değişmeden eklenir (`vpk pack --signParams`) |
| P0-15 | Lisans sunucusu + ödeme (Paddle/iyzico) + aktivasyon | P0-10 | Webhook idempotent; ödeme olmadan lisans yok | ✅ sunucu + etkinleştirme/yenileme/taşıma + Paddle webhook + güncelleme izni (gerçek sunucuyla uçtan uca test) · ⬜ gerçek Paddle hesabıyla deneme, iyzico webhook, anahtarın e-postayla gönderimi, VPS kurulumu |

## P1 — İlk ticari sürümü güçlendirir
| # | İş | Bağımlılık | Kabul kriteri | Durum |
|---|---|---|---|---|
| P1-1 | Tekrarlayan randevular (seri, tek/tüm seri düzenleme) | P0-3 | Seri oluştur/düzenle/iptal testleri; çakışan örnekler raporlanır | ✅ (test) · 🟡 ekran |
| P1-2 | Bekleme listesi (iptalde öneri) | P0-3 | İptal sonrası uygun aday listelenir | ✅ (test) · 🟡 ekran |
| P1-3 | Komisyon/prim motoru (hizmet, paket satışı, uygulayan ayrı) | P0-6, P0-7 | Satan ≠ uygulayan senaryosu doğru prim üretir | ✅ (test) · 🟡 ekran |
| P1-4 | Ürün/malzeme stoku | P0-7 | Seans tamamlamada malzeme düşümü (isteğe bağlı) | ✅ (test) · 🟡 ekran |
| P1-5 | Onam formları ve müşteri fotoğrafları (şifreli dosya deposu, ayrı yetki) | P0-8, hukuki görüş | Yetkisiz kullanıcı göremez; yedekte yer alır | ✅ (test) · 🟡 ekran · ⚖️ metinler |
| P1-6 | SMS (Netgsm, İleti Merkezi, Verimor) + outbox + bütçe + segment sayacı | Faz 5 tasarımı | Sağlayıcı kapalı/yanıtsız → kuyrukta bekler; mükerrer yok | ✅ İleti Merkezi + outbox + aylık limit + boy tahmini (test) · ⬜ Netgsm, Verimor |
| P1-7 | WhatsApp Cloud API (işletmenin hesabı, utility şablonları) | P1-6 altyapısı | Hata kodları eşlenir; limit aşımı durdurur | ✅ (test; gerçek hesapla denenmedi) |
| P1-8 | Google Drive / OneDrive / Dropbox yedek hedefleri | P0-9, KVKK görüşü | Kesinti sonrası devam; kota uyarısı | ✅ eşitleme klasörü yöntemi (test) · ⬜ OAuth API yöntemi |
| P1-9 | Excel/PDF rapor dışa aktarımı, ICS | P0-5 | Türkçe karakterler doğru | ⬜ |
| P1-10 | Kaynak sütunlu zaman ızgarası + sürükle-bırak | P0-12 | 1366×768'de okunur | ⬜ |
| P1-11 | SQLite FTS5 arama (100 bin+ müşteri) | P0-5 | 100 bin kayıtta < 200 ms | ⬜ |
| P1-12 | Kuaför, SPA, danışmanlık profillerinin özel modülleri | Faz 3 | Profil başına gerçek senaryo testleri | 🟡 danışmanlık: firma müşterisi + VKN doğrulama (test) · ⬜ kuaför: boya bekleme süresi (personel boşta, koltuk dolu) · ⬜ SPA: hediye kartı · ⬜ çevrim içi görüşme bağlantısı |
| P1-15 | Hizmete özel oda/cihaz kuralı (lazer → lazer odası + cihaz) | P0-3 | Yanlış oda veya cihazsız randevu kaydedilmez; öneriler diğer uygun odaları tarar | ✅ (test, uçtan uca senaryo dahil) · 🟡 ekran |
| P1-13 | Gizli uzman notları (yazar bazlı, şifreli) | P0-8 | Sekreter göremez; denetim kaydı | ⬜ |
| P1-14 | Arayüz yerelleştirme altyapısı (resx) | P0-12 | İngilizce dil paketi derlenir | ⬜ |

## P2 — Gelecek
Çoklu bilgisayar (yerel API + PostgreSQL, Faz 7) · çok şube ve merkezi yönetim · online randevu portalı · Google/Microsoft takvim senkronu · e-Arşiv/e-Fatura entegratörü · grup dersleri/kontenjan · evcil hayvan, araç, veli modülleri · hediye kartı/üyelik · koyu tema · mobil yardımcı uygulama.

## Bilinen riskler
| Risk | Etki | Azaltma |
|---|---|---|
| Arayüz Windows'ta henüz çalıştırılmadı | Çalışma anı hataları | Smoke test artık her sayfanın her sekmesini çizer; CI + ilk Windows çalıştırmasında rapor |
| Yükleyici imzasız | SmartScreen uyarısı; **Akıllı Uygulama Denetimi açık Windows 11'de program hiç açılmıyor** (yeni bir bilgisayarda görüldü) | OV sertifika + bulut HSM satıştan önce zorunlu; müşteriye güvenlik özelliklerini kapatmasını asla söylememe |
| Stripe Türkiye'de yok | Satış altyapısı gecikmesi | Paddle/iyzico kararı (LICENSING.md) |
| Artifact Signing Türkiye'ye kapalı | SmartScreen uyarıları | OV + bulut HSM; itibar zamanla oluşur |
| Bulut yedek = yurt dışı aktarım (KVKK) | Hukuki risk | Avukat görüşü; varsayılan kapalı; açık uyarı |
| Sağlık profilleri | Mevzuat (MBYS/e-Nabız) | Yalnızca ön büro; tıbbi kayıt yok |
| Yerel deneme sayacı kurcalanabilir | Gelir kaybı | ✅ Sunucu tarafı deneme kaydı (Faz 6); çevrimdışı kalan bilgisayarda hâlâ yerel sayaç geçerli |
