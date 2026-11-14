<?php
/**
 * Title: Program sayfası: Vernuko Teknik Servis
 * Slug: vernuko/urun-teknik-servis
 * Categories: vernuko
 * Description: Vernuko Teknik Servis sayfası ("Geniş sayfa" şablonuyla). Planlanan ekran, planlanan özellikler, satış bölümü (paket tanımlanınca fiyat ve indirme kendiliğinden gelir) ve diğer programlar.
 */
?>
<!-- wp:group {"align":"full","className":"vk-giris","style":{"spacing":{"padding":{"top":"var:preset|spacing|50","bottom":"var:preset|spacing|60"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-group alignfull vk-giris" style="padding-top:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:paragraph {"align":"wide","className":"vk-urun-adi"} -->
<p class="alignwide vk-urun-adi">Vernuko Teknik Servis</p>
<!-- /wp:paragraph -->

<!-- wp:heading {"level":1,"align":"wide","className":"vk-giris__baslik vk-giris__baslik--urun"} -->
<h1 class="wp-block-heading alignwide vk-giris__baslik vk-giris__baslik--urun">Servis fişleriniz <br>kendi bilgisayarınızda.</h1>
<!-- /wp:heading -->

<!-- wp:columns {"align":"wide","style":{"spacing":{"margin":{"top":"clamp(2rem, 4vw, 3.5rem)"},"blockGap":{"top":"2.5rem","left":"clamp(2rem, 5vw, 4.5rem)"}}}} -->
<div class="wp-block-columns alignwide" style="margin-top:clamp(2rem, 4vw, 3.5rem)"><!-- wp:column {"width":"36%","className":"vk-giris__metin"} -->
<div class="wp-block-column vk-giris__metin" style="flex-basis:36%"><!-- wp:paragraph {"className":"vk-giris__alt"} -->
<p class="vk-giris__alt">Cihaz kabulden teslime kadar her adım tek programda. İnternet kesilse de fiş açılır, parça düşülür, teslim yapılır; müşteri ve cihaz bilgisi başka bir şirketin sunucusuna gitmez.</p>
<!-- /wp:paragraph -->

<!-- wp:paragraph {"className":"vk-giris__not"} -->
<p class="vk-giris__not">Telefon, bilgisayar, beyaz eşya ve klima servisleri için geliştiriliyor.</p>
<!-- /wp:paragraph --></div>
<!-- /wp:column -->

<!-- wp:column {"width":"64%"} -->
<div class="wp-block-column" style="flex-basis:64%"><!-- wp:vernuko/pano {"ekran":"servis"} /--></div>
<!-- /wp:column --></div>
<!-- /wp:columns --></div>
<!-- /wp:group -->

<!-- wp:group {"tagName":"section","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:heading {"align":"wide"} -->
<h2 class="wp-block-heading alignwide">Planlananlar</h2>
<!-- /wp:heading -->

<!-- wp:paragraph {"align":"wide","className":"vk-aile__giris"} -->
<p class="alignwide vk-aile__giris">Program çıktığında bu liste değişebilir. Vernuko Randevu'daki temel aynen gelir: internetsiz çalışma, aynı işyerindeki diğer bilgisayarlar, yetkiler ve şifreli yedek.</p>
<!-- /wp:paragraph -->

<!-- wp:html -->
<ul class="vk-soz vk-soz--genis alignwide">
	<li><b>Servis fişi.</b> Cihaz, seri numarası, arıza, aksesuarlar ve teslim alan; fiş ve cihaz etiketi yazdırılır.</li>
	<li><b>Fiyat onayı.</b> Tutar müşteriye SMS ile kendi hesabınızdan gider; onay fişe yazılır.</li>
	<li><b>Parça.</b> Parça stoku, parça bekleyen fişler ve fişe düşülen parçalar.</li>
	<li><b>Cihaz geçmişi.</b> Aynı seri numarasıyla gelen cihazın önceki onarımları ve garanti süresi.</li>
	<li><b>Teslim.</b> Hazır cihazlar listesi, ödeme ve teslim imzası.</li>
</ul>
<!-- /wp:html --></section>
<!-- /wp:group -->

<!-- wp:group {"tagName":"section","anchor":"fiyat","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section id="fiyat" class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:vernuko/urun-satis {"urun":"vernuko-teknik-servis","align":"wide"} /--></section>
<!-- /wp:group -->

<!-- wp:group {"tagName":"section","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:heading {"align":"wide"} -->
<h2 class="wp-block-heading alignwide">Diğer Vernuko programları</h2>
<!-- /wp:heading -->

<!-- wp:vernuko/urunler {"haric":"vernuko-teknik-servis","align":"wide"} /--></section>
<!-- /wp:group -->
