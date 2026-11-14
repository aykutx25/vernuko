# Vernuko satış teması — araştırma ve tasarım planı

## 1. Pazarın görsel dili (14 Kasım 2026, ana sayfa ekran görüntüleri)

| Site | Görünüm | Ne söylüyor |
|---|---|---|
| SalonJet | Mor-lacivert degrade, telefon çerçevesi, uçuşan bildirim balonları, Bricolage Grotesque | "Randevular kendiliğinden dolsun" |
| Salun | Beyaz zemin, mor düğme, sağda gösterge paneli kartı, Plus Jakarta Sans | "Kurumsal yönetim standardı" |
| Arvengo | Ortalanmış başlık, fosforlu kalem vurgusu, Poppins, uygulama mağazası rozetleri | "A'dan Z'ye yönetim programı" |
| Rezzim | Krem zemin, koyu yeşil, berber fotoğrafı + telefon | "Randevu ve kasa programı" |
| Orest, Hursoft (masaüstü) | Eski e-ticaret düzeni, kutu görseli, kalabalık menü | Ürün kodu, kargo, taksit |
| Fresha | Ortalanmış başlık, siyah hap düğme, takvim ekranı | "#1 software" |
| Diş Hekimi Pro | Mavi, uygulama ekranı, "Offline çalışır / veriler sizde" rozetleri | Ücretsiz |

Ortak kalıplar:
- Mor degrade, telefon çerçevesi.
- "Ücretsiz başla" hap düğmesi ve "Kredi kartı gerekmez" onay listesi.
- Aynı yuvarlak kartlarda üçlü özellik ızgarası.
- "Binlerce salon" iddiası.
- Google Analytics / Meta Pixel çerez bandı.

Masaüstü rakipler ise eski görünüyor ve güven vermiyor.

## 2. Vernuko'nun farkı (içerikten gelen)

- Veriler işletmenin kendi bilgisayarında durur; internet kesilse de çalışır.
- Aylık abonelik ve komisyon yok. Lisans bitince program salt okunur olur, hiçbir kayıt silinmez.
- Tek program, birçok meslek. Terimler mesleğe göre değişir: Müşteri / Hasta / Danışan / Müvekkil / Araç sahibi; Koltuk / Ünit / Lift.

Site de bu sözü tutar:
- Ziyaretçiyi izlemez, dış yazı tipi sunucusu çağırmaz.
- Bu yüzden çerez onay bandına gerek kalmaz.

## 3. Tasarım planı

### Renk (uygulamanın kendi tonları + tek sinyal rengi)

| Ad | Hex | Görev |
|---|---|---|
| Mürekkep | `#1B2640` | Metin, başlık |
| Lacivert | `#1F2A44` | Tek koyu bant, birincil düğme (uygulamanın ana rengi) |
| Defter | `#F1F4F8` | Sayfa zemini — soğuk, kâğıt beyazı; krem değil |
| Beyaz | `#FFFFFF` | Pano yüzeyi (uygulamadaki gibi) |
| Kalem mavisi | `#2F5FC4` | Bağlantı, odak |
| Şimdi sarısı | `#F2C230` | Yalnızca "şimdi/bugün" anlamı: panodaki şimdi çizgisi, seçili gün |

Durum renkleri panonun içinde uygulamadakiyle aynıdır (yeşil tamamlandı, amber bekliyor).

### Yazı tipi
Tek aile: **Schibsted Grotesk** (OFL, değişken 400–900, italik). Türkçe harflerin hepsi, ₺ işareti ve tablo rakamları var. Site kendi sunucusundan yükler.

- **Başlıklar:** 800, sıkı harf aralığı (-0,02em). Tür, sayfanın kişiliğini taşır: iri, kalın, kısa cümleler.
- **Metin:** 400, 18 px, satır aralığı 1,55, satır en çok 68 karakter.
- **Ölçek (1,25):** 18 / 22,5 / 28 / 35 / 44 / 56–72.

### Düzen

```
┌ Vernuko   Nasıl çalışır  İşletmeler  Fiyat  Sorular        Lisanslarım  [Deneme sürümünü indir] ┐
│                                                                                               │
│  Randevu defteriniz               ┌ [Kuaför][Güzellik][Klinik][Psikolog][Avukat][Oto servis] ┐│
│  kendi bilgisayarınızda.          │ Çarşamba, 25 Kasım                     8 randevu         ││
│  (iki satır açıklama)             │ 09:00 ▌Ayşe K.   Saç boyama      Selin · Koltuk 2  Geldi ││
│  [Deneme sürümünü indir]          │ 10:00 ▌…  ═══ şimdi 10:42 ═══ (sarı)                     ││
│  Fiyatları gör                    │ 11:15 ▌…                                                 ││
│  Windows 10 ve 11'de çalışır.     │ ● Yerel veritabanı — internetsiz çalışır   İnternet [●○] ││
│                                   └──────────────────────────────────────────────────────────┘│
├──────────────── lacivert bant: Verileriniz nerede durur? (iki yol şeması) ─────────────────────┤
│  Bir iş günü — saat sütunu solda (08:55 … 19:05), her saatte gerçek bir iş                     │
│  Her işletmenin dili ayrı — tipografik tablo (meslek, kişiye ne denir, takvimde ne var)        │
│  Fiyat — fiş: kalem kalem dökümü, komisyon ₺0,00; altında "lisans biterse" sözü                │
│  Sorular — açılır liste                                                                        │
└ alt bilgi: satıcı bilgileri, yasal sayfalar, "bu site sizi izlemez" notu ───────────────────────┘
```

- **Hizalama:** metin sola dayalı; ortalanmış başlık yok.
- **Genişlik:** en çok 1200 px.
- **Mobil:** pano metnin altına iner ve liste olarak kalır.

### İlkeler
1. **Görsel, ürünün kendisidir.** Stok fotoğraf ve telefon çerçevesi yok. Ana görsel, uygulamanın gün ekranının HTML ile çizilmiş canlı halidir: meslek değişince terimler, hizmetler ve kaynaklar değişir. İnternet anahtarı kapatılınca pano çalışmaya devam eder. Cesaret yalnızca burada harcanır.
2. **Saat, yapı taşıdır.** Numaralı adım yerine gerçek saatler kullanılır (iş günü bölümü bir sıradır).
3. **Fiyat bir fiştir.** Kasanın gerçek nesnesi. Gizli maliyet olmadığını kalem kalem gösterir.
4. **Sessiz geri kalan:** tek koyu bant, tek sinyal rengi. Kart ızgarası, degrade, büyük harfli etiket, ortalanmış başlık yok.
5. **Dürüst metin:** doğrulanmamış iddia yok ("binlerce", "#1"). Uygulamada olmayan özellik yazılmaz.
6. **Sözünü tutan site:**
   - Google Fonts, analiz, emoji CDN'i ve Gravatar çağrılmaz.
   - Yalnızca satın alma ve Lisanslarım için gerekli oturum çerezleri var.

### Varsayılanlara karşı gözden geçirme (değiştirilenler)

| İlk aklıma gelen | Neden varsayılan | Yerine |
|---|---|---|
| "Ücretsiz başla" hap düğmesi + "Kredi kartı gerekmez" onay listesi | Pazardaki herkes | "Deneme sürümünü indir" (ne olacağını söylüyor). Altında tek cümle: "30 gün; kart bilgisi istenmez." |
| Üç kartlı özellik ızgarası | SaaS kart kiti | Saat sütunlu iş günü akışı |
| Üç sütun fiyat kartı + "En çok tercih edilen" rozeti | Herkeste aynı | Fiş: paket seçimi + kalem kalem döküm |
| Hero altında "Windows · 2 dk kurulum · 30 gün" meta dizisi | Orta nokta kalıbı | Tam cümle |
| Krem zemin | Yapay zekâ kalıbı #1 (ve Rezzim) | Soğuk "defter" beyazı `#F1F4F8` + uygulama laciverti |
| Sarı vurguyu başlıkta tek kelimeye vermek | Tek kelime vurgusu kalıbı | Sarı yalnızca "şimdi" anlamında, metinde hiç |
| Bölüm üstü küçük büyük harf etiketler | Şablon süsü | Yok; başlık doğrudan |
