# G. Arayüz ve Tasarım Sistemi

Referans: kullanıcının paylaştığı son sade Vernuko ekranı (beyaz/açık gri zemin, lacivert-antrasit yazı, minimal çizgi ikonlar, yumuşak köşeler, kontrollü durum renkleri). Bu karakter korunmuştur; görsel öğeler bu proje için sıfırdan çizilmiştir.

> **Doğrulama durumu:** Arayüz Linux'ta derlenir (XAML markup derlemesi tip/özellik hatalarını yakalar) ve tüm `{Binding}` yolları ile `StaticResource` anahtarları otomatik testte statik olarak kontrol edilir. **Ekranlar bu ortamda görsel olarak çalıştırılamadı** (Windows yok; Wine 9 .NET 10'u çalıştıramıyor). Windows'ta `Vernuko.exe --smoke-test <klasör>` tüm ekranları 1366×768 ve 1920×1080'de PNG olarak üretir ve herhangi bir bağlama hatasında başarısız olur; CI'da Windows işinde çalıştırılır.

## Tasarım tokenları (`src/Vernuko.Desktop/Themes/Colors.xaml`)
| Token | Değer | Kullanım |
|---|---|---|
| AppBackground | `#F5F6F8` | Pencere zemini |
| Surface / SurfaceAlt | `#FFFFFF` / `#F9FAFB` | Kartlar, satırlar / ikincil alan |
| Border / BorderStrong | `#E3E6EB` / `#CDD2DA` | Kart ve giriş kenarları |
| Text / Secondary / Muted | `#1B2640` / `#5B6475` / `#6B7385` | Metin hiyerarşisi |
| Primary | `#1F2A44` | Ana buton, seçili segment |
| Selection / Hover | `#E8EDF6` / `#F1F3F7` | Seçili menü, üzerine gelme |
| Success / Info / Warning / Danger (+Soft) | yeşil / mavi / amber / kırmızı | Yalnızca durum çipleri, sol durum çubuğu, uyarı bantları |

Yazı tipi: Segoe UI Variable (Windows 11) → Segoe UI (Windows 10). Gövde 14 px, başlık 28 px yarı kalın, bölüm 17 px. Neon renk, mor/mavi geçiş, büyük "AI" kartları ve gereksiz animasyon yoktur.

## Bileşen sistemi (`Themes/Controls.xaml`)
Butonlar (Primary, Secondary, Ghost, Danger, Icon, Link, Tile, Nav), segment seçici (Gün/Hafta/Ay), metin kutusu (yer tutucu metinli), parola kutusu, onay kutusu, açılır liste, tarih seçici, ince kaydırma çubukları, liste/tablo (sanallaştırmalı), sekmeler, kart, durum çipi, durum çubuğu, hata/bilgi/uyarı bantları. Kendi 24×24 çizgi ikon seti (`Themes/Icons.xaml`, 45 ikon).

## Ekran haritası
```
Kurulum sihirbazı (10 adım) ─▶ Giriş ─▶ Ana kabuk ──┬─ Ana Sayfa
                                         (kilit ekranı │─ Randevular (Gün / Hafta / Ay + sağ panel / yeni randevu paneli)
                                          üst katman)  │─ Müşteriler (liste + kart: geçmiş, paketler, hesap, kişisel veriler)
                                                       │─ Hizmetler (hizmetler · odalar ve cihazlar · paket tanımları)
                                                       │─ Personel (bilgi, saatler, verdiği hizmetler, izin/kapalı gün)
                                                       │─ Paket & Seanslar (profil açıksa)
                                                       │─ Ödemeler (günlük kasa, hareketler, ters kayıt, açık bakiyeler)
                                                       │─ Raporlar
                                                       └─ Ayarlar (İşletme · Kullanıcılar · Yedekleme · Entegrasyonlar ve
                                                                   Maliyetler · Lisans · Denetim kaydı · Hesabım)
Üst katman diyaloglar: Ödeme Al, Paket Satışı · Onay/soru pencereleri
```
Menü öğeleri **yetkiye ve mesleğe göre** gösterilir (ör. "Paket & Seanslar" yalnızca paket özelliği açık profillerde; sekreter "Kullanıcılar" sekmesini görmez). Terimler profile göre değişir (Müşteriler → Hastalar / Danışanlar …).

## Ana randevu ekranı
- **Üst çubuk:** logo, işletme adı, genel arama (Ctrl+K), kilitle (Ctrl+L), kullanıcı (baş harfler, ad, rol), oturumu kapat, pencere düğmeleri (özel başlık çubuğu, sürükleme ve çift tıkla büyütme).
- **Sol menü** (232 px): Ana Sayfa, Randevular, Müşteriler, Hizmetler, Personel, [Paket & Seanslar], Ödemeler, Raporlar, Ayarlar; altta lisans kartı ve demo uyarısı.
- **Çalışma alanı:** başlık + tarih, Gün/Hafta/Ay, "Yeni Randevu"; 4 küçük özet kartı (günün randevusu, bekleyen, tamamlanan, aktif personel); araç çubuğu (önceki/sonraki, tarih, Bugün, personel/oda/hizmet filtreleri); gün listesi (saat rozeti, durum renk çubuğu, müşteri + telefon, hizmet · seans · süre, personel + oda, durum çipi, ⋯ işlem menüsü), öğle arası satırı, şu anki randevu vurgusu.
- **Sağ sütun:** Hızlı İşlemler, Yaklaşan Randevular, Günün Özeti (tamamlanma halkası). **Yeni randevu açıldığında bu sütunun yerini form alır**; ayrı sayfaya gidilmez. Form kapalıyken takvim geniş kalır.
- **Durum bar:** yerel veritabanı (internetsiz çalışır), son yedekleme (uyarı rengiyle), WhatsApp: Manuel, SMS: Kapalı, Bulut yedekleme: Kapalı.

## Temel kullanıcı akışları
1. **Telefonla randevu (hedef: < 30 sn):** Ctrl+N → müşteri adı/telefonu yaz (canlı arama) → bulunamazsa "Yeni müşteri ekle" (ad/soyad/telefon önceden doldurulur) → hizmet (oda otomatik seçilir) → uygun saat çiplerinden birine tıkla → Ctrl+S. Uyarı varsa (mesai dışı, paket hakkı yetersiz) tek onay; engel varsa (çakışma, izin) açık mesaj.
2. **Seans tamamlama:** satır ⋯ → Müşteri geldi → İşleme başla → Tamamla. Paket hakkı otomatik düşer; yanlışlıkla tamamlanırsa "Tamamlamayı geri al" (gerekçe zorunlu, hak iade).
3. **İptal:** ⋯ → İptal et → neden (zorunlu). Seans hakkı düşmez. "İptali geri al" çakışma yeniden kontrol edilerek mümkündür.
4. **Ödeme:** ⋯ → Ödeme al (tutar önerilir) → yöntem → kaydet. Çift tıklama ikinci kayıt oluşturmaz.
5. **Yedek:** Ayarlar › Yedekleme › Şimdi yedekle / Seçili yedeği doğrula / Geri yükle (onay + güvenlik kopyası + gerekirse kurtarma anahtarı).

## Klavye
Ctrl+K genel arama · Ctrl+N yeni randevu · Ctrl+L kilitle · Ctrl+S formu kaydet · Esc paneli/diyaloğu kapat · F5 yenile · Alt+←/→ önceki/sonraki gün · Alt+T bugün · Enter (giriş/arama). Sekme sırası formlarda yukarıdan aşağıya.

## Erişilebilirlik ve ekranlar
- Kontrast (WCAG 2.x, beyaz zemin): Text 15,0:1 · Secondary 6,0:1 · Muted 4,8:1 · Primary buton üzerindeki beyaz yazı 14,3:1. Durum çipleri kendi açık zeminlerinde: başarı 5,2:1, bilgi 4,9:1, uyarı 5,4:1, tehlike 5,1:1 — hepsi AA (4,5:1) üzeri. Durum yalnızca renkle değil, çip etiketiyle de belirtilir.
- PerMonitorV2 DPI farkındalığı (manifest), `UseLayoutRounding`; minimum pencere 1180×680. 1366×768 ve 1920×1080 smoke-test çıktısında kontrol edilir.
- Otomasyon adları (`AutomationProperties.Name`) arama ve form alanlarında.

## Bilinçli olarak yapılmayanlar
Sürükle-bırak takvim (P1 — hafta görünümünde), kaynak sütunlu zaman ızgarası (P1), koyu tema (P2), animasyonlar.
