<?php
/**
 * [vernuko_fis] — prices as a till receipt: the plans defined in the Vernuko Lisans plugin (Paketler ve satış),
 * itemised, with the costs other programs add on (setup, per user, commission) listed at 0. Picking a plan updates
 * the receipt and the buy button, which opens the plugin's checkout page with that plan.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

defined('ABSPATH') || exit;

function vernuko_tl(int $kurus): string
{
    return number_format($kurus / 100, 2, ',', '.') . ' TL';
}

/** @return array{plans: list<array{id:string,name:string,price:int,devices:int,days:int,edition:string}>, buy: string} */
function vernuko_fis_verisi(): array
{
    if (!class_exists('\Vernuko\License\Checkout')) {
        return ['plans' => [], 'buy' => ''];
    }
    $plans = [];
    foreach (\Vernuko\License\Checkout::plans() as $p) {
        if ($p->kind !== 'new' || $p->priceKurus <= 0) {
            continue; // renewals are bought from Lisanslarım
        }
        $plans[] = ['id' => $p->id, 'name' => $p->name, 'price' => $p->priceKurus, 'devices' => $p->devices, 'days' => $p->days, 'edition' => $p->edition];
    }
    $settings = \Vernuko\License\Checkout::settings();
    return ['plans' => $plans, 'buy' => (string) ($settings['buy_url'] ?? '')];
}

function vernuko_sure_metni(int $days): string
{
    // A no-break space keeps "1 yıl" together on a narrow receipt.
    if ($days % 365 === 0) {
        return ($days / 365) . "\u{00A0}yıl";
    }
    if ($days % 30 === 0) {
        return ($days / 30) . "\u{00A0}ay";
    }
    return $days . "\u{00A0}gün";
}

/** [vernuko_fis] */
function vernuko_fis_shortcode(): string
{
    $v = vernuko_fis_verisi();
    if (!$v['plans']) {
        return current_user_can('manage_options')
            ? '<p class="vk-uyari">Fiyatlar burada görünecek: Vernuko Lisans eklentisini etkinleştirip <em>Paketler ve satış</em> sayfasında en az bir "Yeni lisans" paketi tanımlayın.</p>'
            : '<p>Fiyatlar çok yakında burada.</p>';
    }

    $first = $v['plans'][0];
    $secenekler = '';
    foreach ($v['plans'] as $i => $p) {
        $href = $v['buy'] !== '' ? add_query_arg('paket', $p['id'], $v['buy']) : '';
        $secenekler .= '<label class="vk-fis__secenek"><input type="radio" name="vk-paket" value="' . esc_attr($p['id']) . '"' . ($i === 0 ? ' checked' : '')
            . ' data-ad="' . esc_attr($p['name']) . '" data-fiyat="' . esc_attr(vernuko_tl($p['price'])) . '" data-sure="' . esc_attr(vernuko_sure_metni($p['days'])) . '"'
            . ' data-cihaz="' . (int) $p['devices'] . '" data-href="' . esc_url($href) . '">'
            . '<span class="vk-fis__secenek-ad">' . esc_html($p['name']) . '</span>'
            . '<span class="vk-fis__secenek-ayrinti">' . esc_html(vernuko_tl($p['price'])) . ', ' . (int) $p['devices'] . ' bilgisayar, ' . esc_html(vernuko_sure_metni($p['days'])) . '</span>'
            . '</label>';
    }

    $buy = $v['buy'] !== '' ? add_query_arg('paket', $first['id'], $v['buy']) : '';
    wp_enqueue_script('vernuko-fis');

    return '<div class="vk-fis" data-vk-fis>'
        . (count($v['plans']) > 1 ? '<fieldset class="vk-fis__secim"><legend>Paket seçin</legend>' . $secenekler . '</fieldset>' : '')
        . '<div class="vk-fis__kagit">'
        . '<p class="vk-fis__ust"><span>Vernuko</span><span>Fiyat dökümü</span></p>'
        . '<dl class="vk-fis__kalemler">'
        . '<div><dt>Lisans: <span data-fis="ad">' . esc_html($first['name']) . '</span>, <span data-fis="sure">' . esc_html(vernuko_sure_metni($first['days'])) . '</span></dt><dd data-fis="fiyat">' . esc_html(vernuko_tl($first['price'])) . '</dd></div>'
        . '<div><dt>Bilgisayar sayısı</dt><dd data-fis="cihaz">' . (int) $first['devices'] . '</dd></div>'
        . '<div><dt>Kurulum ve ilk ayar</dt><dd>0,00 TL</dd></div>'
        . '<div><dt>Kullanıcı başına ücret</dt><dd>0,00 TL</dd></div>'
        . '<div><dt>Randevu başına komisyon</dt><dd>0,00 TL</dd></div>'
        . '<div><dt>Müşteri ve randevu sınırı</dt><dd>yok</dd></div>'
        . '<div><dt>SMS, WhatsApp</dt><dd>kendi hesabınızdan</dd></div>'
        . '</dl>'
        . '<p class="vk-fis__toplam"><span>Toplam, KDV dahil</span><strong data-fis="fiyat">' . esc_html(vernuko_tl($first['price'])) . '</strong></p>'
        . '</div>'
        . ($buy !== ''
            ? '<p class="vk-fis__dugme"><a class="wp-element-button" data-fis="href" href="' . esc_url($buy) . '">Bu paketi satın al</a></p>'
            : (current_user_can('manage_options') ? '<p class="vk-uyari">Satın al düğmesi için eklentinin <em>Paketler ve satış</em> sayfasına "Satın alma sayfası" adresini girin.</p>' : ''))
        . '</div>';
}
