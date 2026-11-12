# B. Meslek Profilleri ve Modül Matrisi

## Temel karar
Tüm meslekler **tek bir randevu motorunu** kullanır (`Vernuko.Appointments`). Bir meslek profili yeni bir uygulama değildir; yalnızca şunları seçer:

- hangi **özelliklerin** (modüllerin) açık olacağı (`ProfileFeature`),
- ekranlarda kullanılan **terimler** (Müşteri / Hasta / Danışan / Müvekkil …),
- **önerilen hizmetler, oda/cihaz türleri ve paket tanımları** (kurulum sihirbazında doldurulur),
- **veri hassasiyeti** (mesajlarda hizmet adının gizlenmesi gibi varsayılanlar).

Kaynak: `src/Vernuko.BusinessProfiles/BusinessProfileCatalog.cs`. Yeni meslek eklemek = kataloğa bir kayıt eklemek (+ gerekiyorsa yeni bir özellik modülü).

**"Güzellik ve Lazer Merkezi" tek profildir.** Kurulumda tek seçenek olarak görünür; lazer epilasyon, cilt bakımı, bölgesel incelme vb. alt hizmet olarak tanımlanır.

## Modül matrisi

● varsayılan açık · ○ kullanılmaz · ◐ isteğe bağlı. "Faz 1" sütunu bu sürümde çalışan çekirdeği gösterir.

| Modül / Özellik | Faz 1 | Güzellik & Lazer | Kuaför & Berber | SPA & Masaj | Doktor / Poliklinik | Diş Kliniği | Psikolog / Terapist | Veteriner | Pilates / Fitness | Avukat | Eğitim | Danışmanlık / Muhasebe | Oto Servis | Fotoğraf / Organizasyon | Genel |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Randevu motoru (gün/hafta/ay, çakışma, durumlar) | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● |
| Müşteri kartı | ● | ● | ● | ● | ● (Hasta) | ● (Hasta) | ● (Danışan) | ● (Hayvan sahibi) | ● (Üye) | ● (Müvekkil) | ● (Öğrenci) | ● | ● (Araç sahibi) | ● | ● |
| Personel takvimi + izin | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● |
| Oda | ● | ● | ○ | ● | ● | ○ | ◐ | ● | ○ | ● | ● | ◐ | ○ | ○ | ● |
| Cihaz / ekipman | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ○ | ○ | ● | ● |
| Koltuk / Ünit / Lift / Stüdyo | ● (tür olarak) | ○ | ● koltuk | ○ | ○ | ● ünit | ○ | ○ | ● stüdyo | ○ | ○ | ○ | ● lift/bölme | ● stüdyo | ◐ |
| Hazırlık / temizlik aralığı | ● | ● | ◐ | ● | ◐ | ● | ● | ◐ | ● | ○ | ○ | ○ | ○ | ● | ◐ |
| Seans paketi (kalan hak) | ● | ● | ◐ | ● | ○ | ○ | ● | ◐ | ● | ○ | ● | ● | ○ | ○ | ● |
| Bölge bazlı seans | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| Kapora / ödeme takibi | ● | ● | ● | ● | ◐ | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● |
| Komisyon / prim | Faz 2 | ● | ● | ● | ○ | ◐ | ○ | ○ | ◐ | ○ | ◐ | ○ | ◐ | ○ | ◐ |
| Ürün / malzeme stoku | Faz 2 | ● | ● | ◐ | ○ | ◐ | ○ | ● | ○ | ○ | ○ | ○ | ● | ◐ | ◐ |
| Onam formları / fotoğraf | Faz 2 | ● | ○ | ◐ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ◐ araç foto | ○ | ○ |
| Çoklu hizmet / çoklu personel | Faz 3 | ◐ | ● | ◐ | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | ● | ● | ◐ |
| İşlem/bekleme süreleri (boya) | Faz 3 | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| Tekrarlayan randevu | Faz 3 | ○ | ○ | ○ | ◐ | ● | ● | ○ | ● | ○ | ● | ● | ○ | ○ | ◐ |
| Bekleme listesi | Faz 3 | ● | ● | ◐ | ● | ● | ◐ | ○ | ● | ○ | ○ | ○ | ○ | ○ | ◐ |
| Sıra numarası / hasta kabul | Faz 3 | ○ | ○ | ○ | ● | ◐ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| Gizli uzman notları (yazar bazlı) | izin hazır, Faz 3 | ○ | ○ | ○ | ● | ● | ● | ○ | ○ | ● | ○ | ● | ○ | ○ | ○ |
| Online görüşme bağlantısı | Faz 3 | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ● | ○ | ● | ○ | ○ | ○ |
| Evcil hayvan (sahip başına çok hayvan) | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ |
| Grup dersi / kontenjan / katılım | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ◐ pansiyon | ● | ○ | ● | ○ | ○ | ○ | ○ |
| Veli bilgileri | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ○ | ○ |
| Araç (plaka, geçmiş) | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ |
| Belge kontrol listesi | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ | ● | ○ | ● | ○ |
| Çıkar çatışması ön kontrolü | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● | ○ | ○ | ○ | ○ | ○ |
| Teslim tarihi / hazırlık görevleri | Faz 3 | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ● | ● | ○ |
| Bakım / kontrol / aşı hatırlatma | Faz 5 | ● cihaz bakımı | ○ | ○ | ○ | ● kontrol | ○ | ● aşı | ○ | ○ | ○ | ○ | ● periyodik | ○ | ○ |
| Mesajda hizmet adını gizle | ● | ○ | ○ | ○ | ● | ● | ● | ○ | ○ | ○ | ○ | ○ | ○ | ○ | ○ |

## Profil başına notlar ve sınırlar

- **Güzellik & Lazer:** 8 seanslık paket, iptalde hak düşmez, tamamlamada düşer, bölge bazlı ayrı paket, aynı oda/cihaz çakışan saatte iki müşteriye atanamaz, seans değişiklikleri denetim günlüğünde. Fotoğraf ve onam formları özel nitelikli veri sayılabilir: Faz 2'de ayrı yetki (sekreter fotoğrafları göremez, onay kaydedebilir) ve şifreli dosya deposuyla eklendi; onam metinleri işletmenin hukuk danışmanıyla hazırlanmalıdır. Faz 2 ile ayrıca: hizmet tamamlanınca otomatik malzeme düşümü (ör. jel, tek kullanımlık örtü), ürün satışı, satan/uygulayan ayrı prim, tekrarlayan randevu (ör. 6 haftada bir), bekleme listesi.
- **Doktor / Diş / Psikolog:** Faz 1–3'te yalnızca ön büro. Teşhis, reçete, tıbbi kayıt, e-Nabız/MBYS gibi özellikler mevzuat incelemesi yapılmadan **eklenmeyecek**. Sekreter, terapistin özel notlarını göremez (`PrivateNotesViewOwn` izni yalnızca uzman rolünde; not modülü Faz 3).
- **Avukat:** Dava süresi hesaplama gibi hata riski yüksek otomasyonlar doğrulanmış mevzuat mantığı olmadan eklenmeyecek.
- **Veteriner:** Hayvan sahip değiştirince klinik kayıt yeni sahibe, finansal kayıt eski sahipte kalmalı (ezyVet modeli, Faz 3).
- **Oto servis:** Araç müşteriden bağımsız varlık; finansal kayıtlar asla kademeli silinmez (Faz 1'de tüm finansal tablolar zaten append-only).
- **Eğitim:** Öğrencilerin (çoğunlukla çocuk) verileri için veli rızası ve veri minimizasyonu.

## Yeni meslek ekleme kontrol listesi
1. `BusinessProfileId` ve `BusinessProfileCatalog` kaydı (terimler, özellikler, öneriler, hassasiyet).
2. Gerekli yeni özellik yoksa iş bitti — motor ortak.
3. Yeni özellik gerekiyorsa: ayrı modül (domain + EF yapılandırması + migration + servis + ekran), profil bayrağıyla açılır.
4. Gerçek kullanım senaryosu testleri (`tests/Vernuko.Tests`).
