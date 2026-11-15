<?php
/**
 * Vernuko programları: tanıtım bilgileri ve satış durumu.
 *
 * Kimlikler lisans eklentisindekilerle aynıdır (Vernuko\License\Products). Bir program "satışta" sayılır, eklentide
 * o programa ait fiyatı olan en az bir "Yeni lisans" paketi varsa. Paket eklendiği an ürün sayfası "geliştiriliyor"
 * notu yerine fiyat fişini ve (kurulum dosyası yüklendiyse) indirme düğmesini gösterir; temada bir şey değişmez.
 *
 * Yeni program: "vernuko_tema_urunler" süzgeciyle buraya, "vernuko_license_products" süzgeciyle eklentiye eklenir.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

defined('ABSPATH') || exit;

const VERNUKO_RANDEVU = 'vernuko-desktop';

/**
 * fis: the receipt's lines that differ by program (what other programs charge "per …", what they limit).
 * @return array<string, array{ad:string, kisa:string, sayfa:string, ekran:string, ozet:string, kimler:string, fis:array{komisyon:string, sinir:string}}>
 */
function vernuko_urunler(): array
{
    $urunler = [
        VERNUKO_RANDEVU => [
            'ad' => 'Vernuko Randevu',
            'kisa' => 'Randevu',
            'sayfa' => '/',
            'ekran' => 'randevu',
            'ozet' => 'Randevu, müşteri kartı, paket seansları, kasa ve prim.',
            'kimler' => 'Kuaför, güzellik merkezi, klinik, psikolog, avukat, oto servis',
            'fis' => ['komisyon' => 'Randevu başına komisyon', 'sinir' => 'Müşteri ve randevu sınırı'],
        ],
        'vernuko-stok' => [
            'ad' => 'Vernuko Stok',
            'kisa' => 'Stok',
            'sayfa' => '/stok/',
            'ekran' => 'stok',
            'ozet' => 'Stok, satış, mal kabul, sayım ve cari hesap.',
            'kimler' => 'Hırdavat, kırtasiye, yedek parça, toptancı',
            'fis' => ['komisyon' => 'Satış başına komisyon', 'sinir' => 'Ürün ve cari sınırı'],
        ],
        'vernuko-teknik-servis' => [
            'ad' => 'Vernuko Teknik Servis',
            'kisa' => 'Teknik Servis',
            'sayfa' => '/teknik-servis/',
            'ekran' => 'servis',
            'ozet' => 'Servis fişi, cihaz takibi, parça, fiyat onayı ve teslim.',
            'kimler' => 'Telefon, bilgisayar, beyaz eşya ve klima servisleri',
            'fis' => ['komisyon' => 'Servis fişi başına komisyon', 'sinir' => 'Müşteri ve fiş sınırı'],
        ],
    ];
    return (array) apply_filters('vernuko_tema_urunler', $urunler);
}

/** "Vernuko Stok" as a lockup: the wordmark (alt "Vernuko") and the program's short name; read aloud as one name. */
function vernuko_urun_logosu(string $id): string
{
    $u = vernuko_urun($id);
    if ($u === null) {
        return '';
    }
    return '<span class="vk-urun-logo"><img src="' . esc_url(get_template_directory_uri() . '/assets/img/vernuko-yazi.svg') . '" alt="Vernuko" width="90" height="20"><span>' . esc_html($u['kisa']) . '</span></span>';
}

function vernuko_urun(string $id): ?array
{
    return vernuko_urunler()[$id] ?? null;
}

/** "satista" or "gelistiriliyor" — from the plugin's plans, so the site follows the shop without editing pages. */
function vernuko_urun_durumu(string $id): string
{
    // Without the plugin there are no plans to read; Randevu, the program the site is about, still is on sale.
    $durum = $id === VERNUKO_RANDEVU && !class_exists('\Vernuko\License\Checkout') ? 'satista' : 'gelistiriliyor';
    if (class_exists('\Vernuko\License\Checkout')) {
        foreach (\Vernuko\License\Checkout::plans() as $p) {
            if ($p->kind === 'new' && $p->priceKurus > 0 && ($p->product ?? VERNUKO_RANDEVU) === $id) {
                $durum = 'satista';
                break;
            }
        }
    }
    return (string) apply_filters('vernuko_urun_durumu', $durum, $id);
}

function vernuko_urun_durum_metni(string $durum): string
{
    return $durum === 'satista' ? 'Satışta' : 'Geliştiriliyor';
}

/**
 * [vernuko_urunler] / vernuko/urunler — the family as one ledger: program, what it keeps, for whom, status.
 * $haric: leave one program out (its own page lists "the others").
 */
function vernuko_urunler_html(string $haric = ''): string
{
    $satirlar = '';
    foreach (vernuko_urunler() as $id => $u) {
        if ($id === $haric) {
            continue;
        }
        $durum = vernuko_urun_durumu($id);
        $satirlar .= '<li class="vk-aile__satir" data-durum="' . esc_attr($durum) . '">'
            . '<a class="vk-aile__ad" href="' . esc_url(home_url($u['sayfa'])) . '">' . vernuko_urun_logosu($id) . '</a>'
            . '<span class="vk-aile__ozet">' . esc_html($u['ozet']) . '</span>'
            . '<span class="vk-aile__kimler">' . esc_html($u['kimler']) . '</span>'
            . '<span class="vk-aile__durum">' . esc_html(vernuko_urun_durum_metni($durum)) . '</span>'
            . '</li>';
    }
    return $satirlar === '' ? '' : '<ul class="vk-aile">' . $satirlar . '</ul>';
}

/**
 * vernuko/urun-satis — a product page's buying section. On sale: the price receipt and the download button.
 * Not yet: an honest note (no date is promised, no e-mail is collected).
 */
function vernuko_urun_satis_html(string $id): string
{
    $u = vernuko_urun($id);
    if ($u === null) {
        return '';
    }
    if (vernuko_urun_durumu($id) === 'satista') {
        $indir = shortcode_exists('vernuko_indir') ? do_shortcode('[vernuko_indir urun="' . esc_attr($id) . '"]') : '';
        return '<div class="vk-urun-satis">'
            . '<div class="vk-urun-satis__indir"><h3>30 gün ücretsiz deneyin</h3><p>Kart bilgisi istenmez. Deneme bitince satın aldığınız anahtarı girersiniz; kayıtlar olduğu gibi kalır.</p>' . $indir . '</div>'
            . '<div class="vk-urun-satis__fis">' . vernuko_fis_shortcode(['urun' => $id]) . '</div>'
            . '</div>';
    }
    return '<div class="vk-yakinda" role="note">'
        . '<p class="vk-yakinda__durum">' . esc_html($u['ad']) . ' geliştiriliyor</p>'
        . '<p>Henüz satılmıyor ve indirilemiyor; satış tarihi belli değil. Program Vernuko Randevu ile aynı kurulum dosyasındadır; kurulumda işletme türünü seçersiniz.</p>'
        . '<p>Satış başladığında fiyatı ve indirme düğmesi burada görünür. Lisansı Vernuko Randevu\'nunkinden ayrıdır: her program kendi anahtarıyla çalışır.</p>'
        . '</div>';
}
