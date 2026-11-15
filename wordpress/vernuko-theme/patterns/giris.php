<?php
/**
 * Title: Ana sayfa: giriş ve canlı gün ekranı
 * Slug: vernuko/giris
 * Categories: vernuko
 * Description: Geniş başlık, açıklama ve Vernuko'nun gün ekranının çalışan örneği (işletme türü ve internet anahtarıyla).
 */
?>
<!-- wp:group {"align":"full","className":"vk-giris","style":{"spacing":{"padding":{"top":"var:preset|spacing|50","bottom":"var:preset|spacing|60"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-group alignfull vk-giris" style="padding-top:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:vernuko/urun-logo {"urun":"vernuko-desktop","align":"wide"} /-->

<!-- wp:heading {"level":1,"align":"wide","className":"vk-giris__baslik"} -->
<h1 class="wp-block-heading alignwide vk-giris__baslik">Randevu defteriniz <br>kendi bilgisayarınızda.</h1>
<!-- /wp:heading -->

<!-- wp:columns {"align":"wide","style":{"spacing":{"margin":{"top":"clamp(2rem, 4vw, 3.5rem)"},"blockGap":{"top":"2.5rem","left":"clamp(2rem, 5vw, 4.5rem)"}}}} -->
<div class="wp-block-columns alignwide" style="margin-top:clamp(2rem, 4vw, 3.5rem)"><!-- wp:column {"width":"36%","className":"vk-giris__metin"} -->
<div class="wp-block-column vk-giris__metin" style="flex-basis:36%"><!-- wp:paragraph {"className":"vk-giris__alt"} -->
<p class="vk-giris__alt">Randevu, müşteri kartı, paket seansları ve kasa tek programda. İnternet kesilse de çalışır; kayıtlarınız başka bir şirketin sunucusuna gitmez.</p>
<!-- /wp:paragraph -->

<!-- wp:buttons {"style":{"spacing":{"margin":{"top":"2rem"}}}} -->
<div class="wp-block-buttons" style="margin-top:2rem"><!-- wp:button -->
<div class="wp-block-button"><a class="wp-block-button__link wp-element-button" href="<?php echo esc_url(home_url('/indir/')); ?>">Deneme sürümünü indir</a></div>
<!-- /wp:button -->

<!-- wp:button {"className":"is-style-vk-metin"} -->
<div class="wp-block-button is-style-vk-metin"><a class="wp-block-button__link wp-element-button" href="#fiyat">Fiyatı gör</a></div>
<!-- /wp:button --></div>
<!-- /wp:buttons -->

<!-- wp:paragraph {"className":"vk-giris__not"} -->
<p class="vk-giris__not">Windows 10 ve 11'de çalışır. 30 gün ücretsiz denenir; kart bilgisi istenmez.</p>
<!-- /wp:paragraph --></div>
<!-- /wp:column -->

<!-- wp:column {"width":"64%"} -->
<div class="wp-block-column" style="flex-basis:64%"><!-- wp:vernuko/pano /--></div>
<!-- /wp:column --></div>
<!-- /wp:columns --></div>
<!-- /wp:group -->
