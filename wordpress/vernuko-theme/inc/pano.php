<?php
/**
 * [vernuko_pano] — the home page's live day board: the app's day screen drawn in HTML. Switching the business type
 * changes the words, services and rooms exactly as the program does (BusinessProfileCatalog); switching the internet
 * off keeps it working. People and businesses are invented. Without JavaScript the first business type is shown.
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

function vernuko_pano_durumlar(): array
{
    return ['tamam' => 'Tamamlandı', 'islemde' => 'İşlemde', 'geldi' => 'Geldi', 'onay' => 'Onaylandı', 'bekliyor' => 'Onay bekliyor'];
}

/** The time the "now" line marks (the app draws it at the current time; the demo freezes it mid-morning). */
const VERNUKO_PANO_SIMDI = '10:42';

function vernuko_pano_satirlar_html(array $meslek): string
{
    $durumlar = vernuko_pano_durumlar();
    $html = '';
    $simdiCizildi = false;
    foreach ($meslek['satirlar'] as [$saat, $kisi, $hizmet, $not, $personel, $kaynak, $durum]) {
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

/** [vernuko_pano] */
function vernuko_pano_shortcode(): string
{
    $meslekler = vernuko_pano_meslekler();
    $ilk = array_key_first($meslekler);
    $m = $meslekler[$ilk];

    $dugmeler = '';
    foreach ($meslekler as $anahtar => $x) {
        $dugmeler .= '<button type="button" data-meslek="' . esc_attr($anahtar) . '" aria-pressed="' . ($anahtar === $ilk ? 'true' : 'false') . '">' . esc_html($x['etiket']) . '</button>';
    }

    // Data for the switch (JavaScript builds the rows with textContent; nothing here is HTML).
    $veri = [];
    foreach ($meslekler as $anahtar => $x) {
        $veri[$anahtar] = $x;
    }

    wp_enqueue_script('vernuko-pano');

    return '<figure class="vk-pano" data-vk-pano>'
        . '<div class="vk-pano__meslekler" role="group" aria-label="İşletme türünü seçin">' . $dugmeler . '</div>'
        . '<div class="vk-pano__ekran">'
        . '<div class="vk-pano__ust">'
        . '<span class="vk-pano__isletme">' . esc_html($m['isletme']) . '</span>'
        . '<span class="vk-pano__tarih">Çarşamba, 25 Kasım</span>'
        . '<span class="vk-pano__sayi">' . count($m['satirlar']) . ' randevu</span>'
        . '</div>'
        . '<ol class="vk-pano__liste" aria-live="polite">' . vernuko_pano_satirlar_html($m) . '</ol>'
        . '<div class="vk-pano__alt">'
        . '<span class="vk-pano__baglanti"><span class="vk-isik" aria-hidden="true"></span><span class="vk-pano__baglanti-metin">Yerel veritabanı — internetsiz çalışır</span></span>'
        . '<label class="vk-anahtar"><input type="checkbox" role="switch" checked data-vk-internet> <span>İnternet</span></label>'
        . '</div>'
        . '</div>'
        . '<figcaption>Vernuko\'nun gün ekranından bir örnek. Kişiler ve işletmeler hayal ürünüdür. İnterneti kapatıp deneyin.</figcaption>'
        . '<script type="application/json" class="vk-pano__veri">' . wp_json_encode(['meslekler' => $veri, 'durumlar' => vernuko_pano_durumlar(), 'simdi' => VERNUKO_PANO_SIMDI], JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP) . '</script>'
        . '</figure>';
}
