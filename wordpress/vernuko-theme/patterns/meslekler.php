<?php
/**
 * Title: Ana sayfa: her işletmenin dili ayrı
 * Slug: vernuko/meslekler
 * Categories: vernuko
 * Description: Meslek profilleri: kişiye ne denir, takvimde ne planlanır, neler hazır gelir.
 */
$satirlar = [
    ['Kuaför ve berber', 'Müşteri', 'Personel, koltuk', 'Boyada bekleme süresi, ürün stoku, personel primi'],
    ['Güzellik ve lazer merkezi', 'Müşteri', 'Uzman, oda, cihaz', 'Bölgeye göre seans paketi, onam formu ve fotoğraf, hediye kartı'],
    ['SPA ve masaj', 'Misafir', 'Terapist, terapi odası', 'Hazırlık ve temizlik aralığı, seans paketi'],
    ['Diş kliniği', 'Hasta', 'Diş hekimi, ünit', 'Tekrarlayan kontrol randevusu, bekleme listesi, gizli notlar'],
    ['Doktor ve poliklinik', 'Hasta', 'Doktor, muayene odası', 'Bekleme listesi, gizli notlar. Ön büro içindir; tıbbi kayıt tutmaz.'],
    ['Psikolog ve terapist', 'Danışan', 'Uzman, görüşme odası', 'Haftalık seans, online görüşme bağlantısı, yalnızca yazanın okuduğu notlar'],
    ['Avukatlık bürosu', 'Müvekkil', 'Avukat, toplantı odası', 'Firma kaydı ve vergi numarası, online görüşme, gizli notlar'],
    ['Danışmanlık ve muhasebe', 'Müşteri', 'Danışman, toplantı odası', 'Firma kayıtları, tekrarlayan görüşmeler'],
    ['Oto servis ve ekspertiz', 'Araç sahibi', 'Teknisyen, lift', 'Lift ve teknisyen planı, kapora'],
];
?>
<!-- wp:group {"tagName":"section","anchor":"isletmeler","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|50","bottom":"var:preset|spacing|60"}}},"layout":{"type":"constrained"}} -->
<section id="isletmeler" class="wp-block-group alignfull" style="padding-top:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:group {"align":"wide","layout":{"type":"default"}} -->
<div class="wp-block-group alignwide"><!-- wp:heading -->
<h2 class="wp-block-heading">Her işletmenin dili ayrı</h2>
<!-- /wp:heading -->

<!-- wp:paragraph {"style":{"typography":{"fontSize":"1.25rem"}},"textColor":"gri"} -->
<p class="has-gri-color has-text-color" style="font-size:1.25rem">Kurulumda mesleğinizi seçersiniz. Program ona göre konuşur: gelen kişiye hasta, danışan ya da müvekkil der; koltuk, ünit ya da lift planlar.</p>
<!-- /wp:paragraph -->

<!-- wp:html -->
<table class="vk-meslek">
	<thead><tr><th scope="col">İşletme</th><th scope="col">Gelen kişi</th><th scope="col">Takvimde planlanan</th><th scope="col">Hazır gelenler</th></tr></thead>
	<tbody>
<?php foreach ($satirlar as [$ad, $kisi, $takvim, $hazir]) : ?>
		<tr><th scope="row"><?php echo esc_html($ad); ?></th><td data-baslik="Gelen kişi"><?php echo esc_html($kisi); ?></td><td data-baslik="Takvimde"><?php echo esc_html($takvim); ?></td><td data-baslik="Hazır gelenler"><?php echo esc_html($hazir); ?></td></tr>
<?php endforeach; ?>
	</tbody>
</table>
<!-- /wp:html -->

<!-- wp:paragraph {"fontSize":"kucuk","textColor":"gri"} -->
<p class="has-gri-color has-text-color has-kucuk-font-size">Hepsinde aynı temel var: randevu ve çakışma kontrolü, müşteri kartı, tahsilat ve kapora, yetkiler, şifreli yedek. Listede olmayan bir işiniz varsa "Genel işletme" profiliyle başlarsınız.</p>
<!-- /wp:paragraph --></div>
<!-- /wp:group --></section>
<!-- /wp:group -->
