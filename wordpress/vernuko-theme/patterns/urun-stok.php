<?php
/**
 * Title: Program sayfası: Vernuko Stok
 * Slug: vernuko/urun-stok
 * Categories: vernuko
 * Description: Vernuko Stok sayfası ("Geniş sayfa" şablonuyla). Temsilî ekran, programdaki özellikler, mali fiş notu, satış bölümü (paket tanımlanınca fiyat ve indirme kendiliğinden gelir) ve diğer programlar.
 */
?>
<!-- wp:group {"align":"full","className":"vk-giris","style":{"spacing":{"padding":{"top":"var:preset|spacing|50","bottom":"var:preset|spacing|60"}}},"layout":{"type":"constrained"}} -->
<div class="wp-block-group alignfull vk-giris" style="padding-top:var(--wp--preset--spacing--50);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:vernuko/urun-logo {"urun":"vernuko-stok","align":"wide"} /-->

<!-- wp:heading {"level":1,"align":"wide","className":"vk-giris__baslik vk-giris__baslik--urun"} -->
<h1 class="wp-block-heading alignwide vk-giris__baslik vk-giris__baslik--urun">Rafınız ve kasanız <br>kendi bilgisayarınızda.</h1>
<!-- /wp:heading -->

<!-- wp:columns {"align":"wide","style":{"spacing":{"margin":{"top":"clamp(2rem, 4vw, 3.5rem)"},"blockGap":{"top":"2.5rem","left":"clamp(2rem, 5vw, 4.5rem)"}}}} -->
<div class="wp-block-columns alignwide" style="margin-top:clamp(2rem, 4vw, 3.5rem)"><!-- wp:column {"width":"36%","className":"vk-giris__metin"} -->
<div class="wp-block-column vk-giris__metin" style="flex-basis:36%"><!-- wp:paragraph {"className":"vk-giris__alt"} -->
<p class="vk-giris__alt">Satış, mal kabul, sayım ve cari hesap tek programda. Kasada internet kesilse de satış yapılır; stok ve cari kayıtları başka bir şirketin sunucusuna gitmez.</p>
<!-- /wp:paragraph -->

<!-- wp:paragraph {"className":"vk-giris__not"} -->
<p class="vk-giris__not">Hırdavat, kırtasiye, yedek parça, toptancı ve mağazalar için.</p>
<!-- /wp:paragraph --></div>
<!-- /wp:column -->

<!-- wp:column {"width":"64%"} -->
<div class="wp-block-column" style="flex-basis:64%"><!-- wp:vernuko/pano {"ekran":"stok"} /--></div>
<!-- /wp:column --></div>
<!-- /wp:columns --></div>
<!-- /wp:group -->

<!-- wp:group {"tagName":"section","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:heading {"align":"wide"} -->
<h2 class="wp-block-heading alignwide">Programda neler var</h2>
<!-- /wp:heading -->

<!-- wp:paragraph {"align":"wide","className":"vk-aile__giris"} -->
<p class="alignwide vk-aile__giris">Vernuko Randevu ile aynı kurulum dosyasıdır; kurulumda işletme türünüzü seçersiniz. Temel aynıdır: internetsiz çalışma, aynı işyerindeki diğer bilgisayarlar, yetkiler ve şifreli yedek.</p>
<!-- /wp:paragraph -->

<!-- wp:html -->
<ul class="vk-soz vk-soz--genis alignwide">
	<li><b>Barkodla satış.</b> Okuyucuyla ya da elle; aynı ürün tekrar okutulunca adet artar. Nakit, kart, havale ya da müşterinin hesabına (veresiye).</li>
	<li><b>Mal kabul ve tedarikçi.</b> Fatura veya irsaliye numarasıyla, KDV hariç maliyetle; tedarikçinin hesabı ve ödemesi.</li>
	<li><b>Sayım.</b> Rafta sayılanla kayıttaki yan yana; fark gerekçesiyle kaydedilir, eski kayıt silinmez.</li>
	<li><b>Kritik seviye ve sipariş önerisi.</b> Kritik seviyenin altına düşen ürünler tedarikçiye göre toplanır.</li>
	<li><b>Gün sonu ve iade.</b> Nakit, kart, havale ve veresiye ayrı; KDV oranına göre döküm. İade edilen ürün stoğa döner.</li>
	<li><b>Birden fazla bilgisayar.</b> Aynı işyerindeki bilgisayarlar ana bilgisayara yerel ağdan bağlanır.</li>
</ul>
<!-- /wp:html -->
<!-- wp:paragraph {"align":"wide","className":"vk-aile__giris"} -->
<p class="alignwide vk-aile__giris"><b>Mali fiş kesmez.</b> Perakende satışta fiş, yazar kasa POS'tan (ÖKC) ya da e-Arşiv faturadan düzenlenir. Vernuko'nun ÖKC bağlantısı ve e-Arşiv'i henüz yok; programı nasıl kullanacağınızı mali müşavirinize danışın.</p>
<!-- /wp:paragraph --></section>
<!-- /wp:group -->

<!-- wp:group {"tagName":"section","anchor":"fiyat","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section id="fiyat" class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:vernuko/urun-satis {"urun":"vernuko-stok","align":"wide"} /--></section>
<!-- /wp:group -->

<!-- wp:group {"tagName":"section","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:heading {"align":"wide"} -->
<h2 class="wp-block-heading alignwide">Diğer Vernuko programları</h2>
<!-- /wp:heading -->

<!-- wp:vernuko/urunler {"haric":"vernuko-stok","align":"wide"} /--></section>
<!-- /wp:group -->
