<?php
/**
 * [vernuko_pano ekran="randevu|stok|servis"] / vernuko/pano — a program's day screen drawn in HTML. Switching the
 * business type changes the words, items and places; switching the internet off keeps it working. People, goods and
 * businesses are invented. Without JavaScript the first business type is shown.
 *
 * "randevu" is Vernuko Randevu's real day screen (BusinessProfileCatalog). "stok" and "servis" are the planned screens
 * of programs that do not exist yet; their caption says so.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

defined('ABSPATH') || exit;

/** @return array<string, array<string, mixed>> */
function vernuko_pano_meslekler(): array
{
    // durum: tamam | islemde | geldi | onay | bekliyor (the app's status names)
    return [
        'kuafor' => [
            'etiket' => 'Kuaför',
            'isletme' => 'Saç Atölyesi',
            'kisi' => 'müşteri',
            'satirlar' => [
                ['09:00', 'Elif Aydın', 'Saç kesimi, 30 dk', '', 'Selin', 'Koltuk 1', 'tamam'],
                ['09:30', 'Hande Yalçın', 'Saç boyama, 90 dk', 'Boya bekleme 10:00–10:40, Selin bu arada boşta', 'Selin', 'Koltuk 2', 'islemde'],
                ['09:35', 'Mert Demir', 'Sakal tıraşı, 20 dk', '', 'Burak', 'Koltuk 1', 'tamam'],
                ['10:00', 'Zeynep Ak', 'Fön, 30 dk', 'Boya beklerken araya alındı', 'Selin', 'Koltuk 1', 'tamam'],
                ['11:30', 'Can Yılmaz', 'Saç kesimi, 30 dk', '', 'Burak', 'Koltuk 1', 'onay'],
                ['13:00', 'Derya Koç', 'Keratin bakımı, 120 dk', 'Bakım paketi: 3. seans', 'Selin', 'Koltuk 2', 'bekliyor'],
            ],
        ],
        'guzellik' => [
            'etiket' => 'Güzellik ve lazer',
            'isletme' => 'Işıl Güzellik',
            'kisi' => 'müşteri',
            'satirlar' => [
                ['09:00', 'Ayşe Kara', 'Lazer epilasyon, bacak', 'Paket: 4. seans / 8', 'Derya', 'Lazer Odası 1, Lazer Cihazı', 'tamam'],
                ['10:00', 'Zeynep Ak', 'Cilt bakımı, 60 dk', 'Onam formu imzalı', 'Merve', 'Cilt Bakım Odası', 'islemde'],
                ['10:30', 'Elif Yılmaz', 'Lazer epilasyon, koltuk altı', 'Paket: 2. seans / 8', 'Derya', 'Lazer Odası 2, Lazer Cihazı', 'geldi'],
                ['11:15', 'Sema Kaya', 'Bölgesel incelme, 45 dk', '', 'Merve', 'Lazer Odası 1', 'onay'],
                ['13:30', 'Ceren Kılıç', 'Medikal cilt bakımı', 'Hediye kartıyla ödeyecek', 'Merve', 'Cilt Bakım Odası', 'bekliyor'],
            ],
        ],
        'dis' => [
            'etiket' => 'Diş kliniği',
            'isletme' => 'Gülümse Diş',
            'kisi' => 'hasta',
            'satirlar' => [
                ['09:00', 'Mehmet Öz', 'Muayene, 20 dk', '', 'Dt. Ayla Şen', 'Ünit 1', 'tamam'],
                ['09:30', 'Fatma Er', 'Diş taşı temizliği, 40 dk', '6 ayda bir, tekrarlayan', 'Dt. Ayla Şen', 'Ünit 1', 'tamam'],
                ['10:20', 'Ali Vural', 'Kontrol', 'Hatırlatma SMS ile gitti', 'Dt. Kerem Aslan', 'Ünit 2', 'islemde'],
                ['11:00', 'Nur Ekin', 'Muayene, 20 dk', '', 'Dt. Kerem Aslan', 'Ünit 2', 'onay'],
                ['14:00', 'Okan Tuna', 'Diş taşı temizliği, 40 dk', 'Bekleme listesinden', 'Dt. Ayla Şen', 'Ünit 1', 'bekliyor'],
            ],
        ],
        'psikolog' => [
            'etiket' => 'Psikolog',
            'isletme' => 'Denge Danışmanlık',
            'kisi' => 'danışan',
            'satirlar' => [
                ['09:00', 'Deniz Acar', 'Bireysel terapi, 50 dk', 'Her hafta, 6. seans', 'Uzm. Psk. Ece Tan', 'Görüşme Odası', 'tamam'],
                ['10:00', 'Selin Kurt', 'Online görüşme, 50 dk', 'Görüşme bağlantısı hatırlatmada', 'Uzm. Psk. Ece Tan', 'Online', 'islemde'],
                ['11:00', 'Mert ve Bora Yıldız', 'Çift terapisi, 60 dk', 'Gizli notu yalnızca yazan uzman görür', 'Psk. Can Ilgın', 'Görüşme Odası', 'geldi'],
                ['13:00', 'Aslı Tekin', 'Bireysel terapi, 50 dk', 'Seans paketi: 3 hak kaldı', 'Uzm. Psk. Ece Tan', 'Görüşme Odası', 'onay'],
            ],
        ],
        'avukat' => [
            'etiket' => 'Avukat',
            'isletme' => 'Tok ve Ural Hukuk',
            'kisi' => 'müvekkil',
            'satirlar' => [
                ['09:30', 'Kaya İnşaat Ltd. Şti.', 'Danışmanlık görüşmesi, 60 dk', 'Firma kaydı, vergi no doğrulandı', 'Av. Selim Tok', 'Toplantı Odası', 'tamam'],
                ['11:00', 'Gül Arıkan', 'Ön görüşme, 30 dk', '', 'Av. Deniz Ural', 'Toplantı Odası', 'geldi'],
                ['14:00', 'Emre Saral', 'Online görüşme, 30 dk', 'Görüşme bağlantısı hatırlatmada', 'Av. Selim Tok', 'Online', 'onay'],
                ['15:30', 'Bilge Gıda A.Ş.', 'Danışmanlık görüşmesi, 60 dk', '', 'Av. Deniz Ural', 'Toplantı Odası', 'bekliyor'],
            ],
        ],
        'oto' => [
            'etiket' => 'Oto servis',
            'isletme' => 'Usta Oto Servis',
            'kisi' => 'araç sahibi',
            'satirlar' => [
                ['08:30', 'Hakan Polat', 'Periyodik bakım, 120 dk', '', 'Murat', 'Lift 1', 'tamam'],
                ['09:00', 'Serkan Uz', 'Ekspertiz, 60 dk', '', 'Oğuz', 'Lift 2', 'tamam'],
                ['10:30', 'Gamze Ateş', 'Periyodik bakım, 120 dk', 'Kapora alındı', 'Murat', 'Lift 1', 'islemde'],
                ['11:00', 'Kemal Işık', 'Ekspertiz, 60 dk', '', 'Oğuz', 'Lift 2', 'geldi'],
                ['14:00', 'Nihan Sezer', 'Periyodik bakım, 120 dk', '', 'Murat', 'Lift 1', 'onay'],
            ],
        ],
    ];
}

/**
 * The app's status names, as tones: tamam (green) | islemde (blue) | geldi (navy) | onay (grey) | bekliyor (amber).
 * Each screen gives the tones its own words.
 */
function vernuko_pano_durumlar(): array
{
    return ['tamam' => 'Tamamlandı', 'islemde' => 'İşlemde', 'geldi' => 'Geldi', 'onay' => 'Onaylandı', 'bekliyor' => 'Onay bekliyor'];
}

/** @return array<string, array<string, mixed>> */
function vernuko_pano_stok_isletmeleri(): array
{
    // Rows: [time, item, movement, note, who/where, place, tone]. After the "now" line: what is expected today.
    return [
        'hirdavat' => [
            'etiket' => 'Hırdavat',
            'isletme' => 'Yıldız Hırdavat',
            'satirlar' => [
                ['08:45', 'Matkap ucu seti, 13 parça', 'Satış, 2 adet', 'Kalan 4; kritik seviye 5', 'Kasa 1', 'Raf A-03', 'bekliyor'],
                ['09:20', 'Silikon, şeffaf', 'Mal kabul, 48 adet', 'Ece Yapı, irsaliye 4471', 'Depo', 'Raf C-11', 'geldi'],
                ['09:55', 'Vida 4×40, 100\'lü kutu', 'Satış, 6 kutu', 'Usta Elektrik carisine, 30 gün vadeli', 'Kasa 1', 'Raf B-07', 'tamam'],
                ['10:30', 'Boya rulosu, 25 cm', 'Sayım', 'Kayıtta 18, rafta 17: fark kaydedildi', 'Depo', 'Raf D-02', 'islemde'],
                ['14:00', 'Su terazisi, 60 cm', 'Beklenen mal, 10 adet', 'Kritik seviyeden sipariş önerisi', 'Satın alma', 'Raf A-09', 'onay'],
            ],
        ],
        'kirtasiye' => [
            'etiket' => 'Kırtasiye',
            'isletme' => 'Kalem Kırtasiye',
            'satirlar' => [
                ['09:05', 'A4 fotokopi kâğıdı', 'Satış, 5 paket', 'Okul siparişi, faturalı', 'Kasa', 'Depo 1', 'tamam'],
                ['09:40', 'Kurşun kalem, 12\'li', 'Satış, 20 kutu', 'Kalan 3; kritik seviye 10', 'Kasa', 'Raf 2', 'bekliyor'],
                ['10:15', 'Dosya, şeffaf', 'Mal kabul, 200 adet', 'Barkodla tek tek okutuldu', 'Depo', 'Raf 5', 'geldi'],
                ['13:30', 'Defter, 80 yaprak', 'Hazırlanacak sipariş, 60 adet', 'Ata İlkokulu, teslim yarın', 'Depo', 'Raf 1', 'onay'],
            ],
        ],
        'parca' => [
            'etiket' => 'Yedek parça',
            'isletme' => 'Kaya Oto Yedek Parça',
            'satirlar' => [
                ['08:30', 'Fren balatası, ön', 'Satış, 1 takım', 'Usta Oto Servis carisine', 'Tezgâh', 'Raf F-12', 'tamam'],
                ['09:10', 'Yağ filtresi', 'Mal kabul, 30 adet', 'Lot 2611; fatura eşleşti', 'Depo', 'Raf Y-04', 'geldi'],
                ['10:05', 'Silecek, 55 cm', 'Sayım', 'Kayıtla raf aynı: 14', 'Depo', 'Raf S-01', 'islemde'],
                ['10:20', 'Ampul H7', 'Satış, 4 adet', 'Kalan 2; kritik seviye 6', 'Tezgâh', 'Raf E-08', 'bekliyor'],
                ['15:00', 'Triger seti', 'Beklenen mal, 3 takım', 'Tedarikçi siparişi verildi', 'Satın alma', 'Raf T-02', 'onay'],
            ],
        ],
    ];
}

/** @return array<string, array<string, mixed>> */
function vernuko_pano_servis_isletmeleri(): array
{
    // Rows: [time, customer, device and fault, note, technician, ticket, tone].
    return [
        'telefon' => [
            'etiket' => 'Telefon ve tablet',
            'isletme' => 'Sinyal Teknik Servis',
            'satirlar' => [
                ['09:05', 'Serdar Aksoy', 'Telefon, ekran kırık', 'Fiyat SMS ile onaylandı', 'Okan', 'Fiş 1124', 'islemde'],
                ['09:30', 'Gizem Er', 'Tablet, şarj soketi', 'Parça siparişte, 2 gün', 'Okan', 'Fiş 1125', 'bekliyor'],
                ['10:10', 'Burak Tan', 'Telefon, batarya değişimi', 'Garanti 6 ay; teslimde imza', 'Elif', 'Fiş 1126', 'tamam'],
                ['10:35', 'Melis Kara', 'Telefon, sıvı teması', 'Arıza tespiti ücretli, onay bekleniyor', 'Elif', 'Fiş 1127', 'onay'],
                ['14:00', 'Kaan Uslu', 'Teslim randevusu', 'Ödeme teslimde, kartla', 'Elif', 'Fiş 1119', 'tamam'],
            ],
        ],
        'beyaz' => [
            'etiket' => 'Beyaz eşya',
            'isletme' => 'Usta Beyaz Eşya Servisi',
            'satirlar' => [
                ['08:30', 'Ayten Duru', 'Çamaşır makinesi, su almıyor', 'Adreste; pompa değişti', 'Hasan', 'Fiş 2210', 'tamam'],
                ['09:45', 'Fikret Oral', 'Buzdolabı, soğutmuyor', 'Gaz kaçağı; parça bekleniyor', 'Hasan', 'Fiş 2211', 'bekliyor'],
                ['10:20', 'Nalan Yüce', 'Bulaşık makinesi', 'Atölyeye alındı', 'Cem', 'Fiş 2212', 'geldi'],
                ['13:00', 'Sevgi Can', 'Fırın, ısıtmıyor', 'Adres ziyareti, ilçe dışı', 'Cem', 'Fiş 2213', 'onay'],
            ],
        ],
        'bilgisayar' => [
            'etiket' => 'Bilgisayar',
            'isletme' => 'Bit Bilgisayar',
            'satirlar' => [
                ['09:00', 'Ece Muhasebe', 'Dizüstü, açılmıyor', 'Firma cari; 3 cihaz aynı fişte', 'Tolga', 'Fiş 3301', 'islemde'],
                ['09:50', 'Arda Şen', 'Masaüstü, veri kurtarma', 'Disk müşterinin onayıyla kopyalandı', 'Tolga', 'Fiş 3302', 'tamam'],
                ['10:25', 'İpek Yalın', 'Dizüstü, klavye', 'Parça siparişte', 'Sena', 'Fiş 3303', 'bekliyor'],
                ['15:30', 'Mert Avcı', 'Teslim alınacak cihaz', 'Kurye ile gelecek', 'Sena', 'Fiş 3304', 'geldi'],
            ],
        ],
    ];
}

/**
 * Screens: the status words, what a row is called, what the offline row says, the connection lines and the caption.
 * @return array<string, array<string, mixed>>
 */
function vernuko_pano_ekranlar(): array
{
    return [
        'randevu' => [
            'profiller' => vernuko_pano_meslekler(),
            'durumlar' => vernuko_pano_durumlar(),
            'birim' => 'randevu',
            'cevrimdisi' => ['saat' => '12:15', 'baslik' => 'Kapıdan gelen {kisi}', 'alt' => 'İnternetsiz kaydedildi', 'durum' => 'onay'],
            'acik' => 'Yerel veritabanı — internetsiz çalışır',
            'kapali' => 'İnternet yok. Randevu, kasa ve yedek bu bilgisayarda çalışmaya devam ediyor.',
            'altyazi' => 'Vernuko Randevu\'nun gün ekranından bir örnek. Kişiler ve işletmeler hayal ürünüdür. İnterneti kapatıp deneyin.',
        ],
        'stok' => [
            'profiller' => vernuko_pano_stok_isletmeleri(),
            'durumlar' => ['tamam' => 'Satıldı', 'geldi' => 'Mal kabul', 'islemde' => 'Sayımda', 'onay' => 'Bekleniyor', 'bekliyor' => 'Kritik seviye'],
            'birim' => 'hareket',
            'cevrimdisi' => ['saat' => '10:41', 'baslik' => 'Kasadan satış', 'alt' => 'İnternetsiz kaydedildi; stok düştü', 'durum' => 'tamam'],
            'acik' => 'Yerel veritabanı — internetsiz çalışır',
            'kapali' => 'İnternet yok. Satış, stok ve yedek bu bilgisayarda çalışmaya devam ediyor.',
            'altyazi' => 'Vernuko Stok geliştiriliyor: bu ekran planlanan tasarımdır, program henüz satışta değil. Ürünler ve işletmeler hayal ürünüdür.',
        ],
        'servis' => [
            'profiller' => vernuko_pano_servis_isletmeleri(),
            'durumlar' => ['tamam' => 'Hazır', 'geldi' => 'Teslim alındı', 'islemde' => 'Onarımda', 'onay' => 'Onay bekliyor', 'bekliyor' => 'Parça bekliyor'],
            'birim' => 'servis fişi',
            'cevrimdisi' => ['saat' => '10:41', 'baslik' => 'Kapıdan gelen cihaz', 'alt' => 'İnternetsiz servis fişi açıldı', 'durum' => 'geldi', 'kaynak' => 'Yeni fiş'],
            'acik' => 'Yerel veritabanı — internetsiz çalışır',
            'kapali' => 'İnternet yok. Servis fişi, kasa ve yedek bu bilgisayarda çalışmaya devam ediyor.',
            'altyazi' => 'Vernuko Teknik Servis geliştiriliyor: bu ekran planlanan tasarımdır, program henüz satışta değil. Kişiler ve işletmeler hayal ürünüdür.',
        ],
    ];
}

/** The time the "now" line marks (the app draws it at the current time; the demo freezes it mid-morning). */
const VERNUKO_PANO_SIMDI = '10:42';

function vernuko_pano_satirlar_html(array $profil, array $durumlar): string
{
    $html = '';
    $simdiCizildi = false;
    foreach ($profil['satirlar'] as [$saat, $kisi, $hizmet, $not, $personel, $kaynak, $durum]) {
        if (!$simdiCizildi && strcmp($saat, VERNUKO_PANO_SIMDI) > 0) {
            $html .= vernuko_pano_simdi_html();
            $simdiCizildi = true;
        }
        $html .= '<li class="vk-satir" data-durum="' . esc_attr($durum) . '">'
            . '<span class="vk-satir__saat">' . esc_html($saat) . '</span>'
            . '<span class="vk-satir__kisi"><b>' . esc_html($kisi) . '</b><span>' . esc_html($hizmet) . '</span>'
            . ($not !== '' ? '<span class="vk-satir__not">' . esc_html($not) . '</span>' : '') . '</span>'
            . '<span class="vk-satir__kaynak"><span>' . esc_html($personel) . '</span><span>' . esc_html($kaynak) . '</span></span>'
            . '<span class="vk-durum">' . esc_html($durumlar[$durum] ?? $durum) . '</span>'
            . '</li>';
    }
    return $html . ($simdiCizildi ? '' : vernuko_pano_simdi_html());
}

function vernuko_pano_simdi_html(): string
{
    return '<li class="vk-simdi"><span>' . esc_html(VERNUKO_PANO_SIMDI) . '</span></li>';
}

/** [vernuko_pano ekran="stok"] */
function vernuko_pano_shortcode($atts = []): string
{
    $atts = shortcode_atts(['ekran' => 'randevu'], is_array($atts) ? $atts : [], 'vernuko_pano');
    $ekranlar = vernuko_pano_ekranlar();
    $ad = isset($ekranlar[$atts['ekran']]) ? (string) $atts['ekran'] : 'randevu';
    $e = $ekranlar[$ad];
    $ilk = array_key_first($e['profiller']);
    $m = $e['profiller'][$ilk];

    $dugmeler = '';
    foreach ($e['profiller'] as $anahtar => $x) {
        $dugmeler .= '<button type="button" data-meslek="' . esc_attr($anahtar) . '" aria-pressed="' . ($anahtar === $ilk ? 'true' : 'false') . '">' . esc_html($x['etiket']) . '</button>';
    }

    wp_enqueue_script('vernuko-pano');

    // Data for the switch (JavaScript builds the rows with textContent; nothing here is HTML).
    $veri = ['meslekler' => $e['profiller'], 'durumlar' => $e['durumlar'], 'simdi' => VERNUKO_PANO_SIMDI, 'birim' => $e['birim'], 'cevrimdisi' => $e['cevrimdisi'], 'acik' => $e['acik'], 'kapali' => $e['kapali']];

    return '<figure class="vk-pano" data-vk-pano data-ekran="' . esc_attr($ad) . '">'
        . '<div class="vk-pano__meslekler" role="group" aria-label="İşletme türünü seçin">' . $dugmeler . '</div>'
        . '<div class="vk-pano__ekran">'
        . '<div class="vk-pano__ust">'
        . '<span class="vk-pano__isletme">' . esc_html($m['isletme']) . '</span>'
        . '<span class="vk-pano__tarih">Çarşamba, 25 Kasım</span>'
        . '<span class="vk-pano__sayi">' . count($m['satirlar']) . ' ' . esc_html($e['birim']) . '</span>'
        . '</div>'
        . '<ol class="vk-pano__liste" aria-live="polite">' . vernuko_pano_satirlar_html($m, $e['durumlar']) . '</ol>'
        . '<div class="vk-pano__alt">'
        . '<span class="vk-pano__baglanti"><span class="vk-isik" aria-hidden="true"></span><span class="vk-pano__baglanti-metin">' . esc_html($e['acik']) . '</span></span>'
        . '<label class="vk-anahtar"><input type="checkbox" role="switch" checked data-vk-internet> <span>İnternet</span></label>'
        . '</div>'
        . '</div>'
        . '<figcaption>' . esc_html($e['altyazi']) . '</figcaption>'
        . '<script type="application/json" class="vk-pano__veri">' . wp_json_encode($veri, JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP) . '</script>'
        . '</figure>';
}
