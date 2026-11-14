<?php
/**
 * Title: Alt bölüm
 * Slug: vernuko/alt
 * Categories: vernuko
 * Inserter: false
 */
?>
<!-- wp:group {"tagName":"footer","className":"vk-alt","style":{"spacing":{"padding":{"top":"var:preset|spacing|50","bottom":"var:preset|spacing|50"}}},"layout":{"type":"constrained"}} -->
<footer class="wp-block-group vk-alt" style="padding-top:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--50)"><!-- wp:columns {"align":"wide","style":{"spacing":{"blockGap":{"top":"2rem","left":"2.5rem"}}}} -->
<div class="wp-block-columns alignwide"><!-- wp:column {"width":"34%"} -->
<div class="wp-block-column" style="flex-basis:34%"><!-- wp:html -->
<a class="vk-logo" href="<?php echo esc_url(home_url('/')); ?>"><img src="<?php echo esc_url(get_template_directory_uri() . '/assets/img/vernuko.png'); ?>" alt="" width="34" height="34"><span>Vernuko</span></a>
<!-- /wp:html -->

<!-- wp:paragraph {"className":"vk-alt__iz"} -->
<p class="vk-alt__iz">Bu site sizi izlemez: analiz, reklam ve dış yazı tipi yok. Yalnızca satın alma ve Lisanslarım için gereken çerezler kullanılır.</p>
<!-- /wp:paragraph --></div>
<!-- /wp:column -->

<!-- wp:column -->
<div class="wp-block-column"><!-- wp:heading {"level":2} -->
<h2 class="wp-block-heading">Vernuko</h2>
<!-- /wp:heading -->

<!-- wp:html -->
<ul>
	<li><a href="<?php echo esc_url(home_url('/indir/')); ?>">İndir</a></li>
	<li><a href="<?php echo esc_url(home_url('/#fiyat')); ?>">Fiyat</a></li>
	<li><a href="<?php echo esc_url(home_url('/#sorular')); ?>">Sorular</a></li>
	<li><a href="<?php echo esc_url(home_url('/lisanslarim/')); ?>">Lisanslarım</a></li>
</ul>
<!-- /wp:html --></div>
<!-- /wp:column -->

<!-- wp:column -->
<div class="wp-block-column"><!-- wp:heading {"level":2} -->
<h2 class="wp-block-heading">Yasal</h2>
<!-- /wp:heading -->

<!-- wp:html -->
<ul>
	<li><a href="<?php echo esc_url(home_url('/on-bilgilendirme-formu/')); ?>">Ön bilgilendirme formu</a></li>
	<li><a href="<?php echo esc_url(home_url('/mesafeli-satis-sozlesmesi/')); ?>">Mesafeli satış sözleşmesi</a></li>
	<li><a href="<?php echo esc_url(home_url('/kvkk-aydinlatma-metni/')); ?>">KVKK aydınlatma metni</a></li>
	<li><a href="<?php echo esc_url(home_url('/cerez-politikasi/')); ?>">Çerez politikası</a></li>
</ul>
<!-- /wp:html --></div>
<!-- /wp:column -->

<!-- wp:column {"width":"28%"} -->
<div class="wp-block-column" style="flex-basis:28%"><!-- wp:heading {"level":2} -->
<h2 class="wp-block-heading">Satıcı</h2>
<!-- /wp:heading -->

<!-- wp:paragraph -->
<p>[Ticari unvan]<br>[Açık adres]<br>[Vergi dairesi ve numarası]<br>[MERSİS numarası]<br>[E-posta]<br>[Telefon]</p>
<!-- /wp:paragraph --></div>
<!-- /wp:column --></div>
<!-- /wp:columns -->

<!-- wp:paragraph {"align":"wide","style":{"spacing":{"margin":{"top":"2.5rem"}}}} -->
<p class="alignwide" style="margin-top:2.5rem">© <?php echo esc_html(wp_date('Y')); ?> Vernuko. Program açık kaynaktır (AGPL-3.0).</p>
<!-- /wp:paragraph --></footer>
<!-- /wp:group -->
