<?php
/**
 * Title: Ana sayfa: sorular
 * Slug: vernuko/sorular
 * Categories: vernuko
 * Description: Sık sorulan sorular (açılır liste).
 */
$sorular = [
    ['İnternet olmadan gerçekten çalışıyor mu?', 'Evet. Randevu, müşteri, kasa, paket ve yedek tamamen bu bilgisayarda çalışır. Lisans, internet olduğunda kendiliğinden yenilenir; bağlantı uzun süre hiç olmazsa (yenileme tarihinden sonra 14 gün) program salt okunur moda geçer. Kayıtlar silinmez.'],
    ['Mac\'te, tablette veya telefonda çalışır mı?', 'Hayır. Vernuko Windows 10 ve 11 için yazıldı. Telefon uygulaması yok.'],
    ['Birden fazla bilgisayarda kullanabilir miyim?', 'Evet. Bir bilgisayar ana bilgisayar olur, aynı işyeri ağındaki diğerleri ona bağlanır. Herkes aynı takvimi görür. Kaç bilgisayar bağlanacağı lisansınızdaki sayıyla belirlenir.'],
    ['Bilgisayar bozulursa ne olur?', 'Yedekler şifrelidir ve sizin seçtiğiniz yerdedir: USB bellek, harici disk ya da OneDrive, Google Drive, Dropbox klasörünüz. Yeni bilgisayara Vernuko\'yu kurarken "yedekten geri yükle" seçilir ve kurtarma anahtarı girilir.'],
    ['Müşterilere SMS veya WhatsApp hatırlatması gider mi?', 'İsterseniz. Kendi İleti Merkezi (SMS) veya WhatsApp Business hesabınızı bağlarsınız; mesajlar oradan, onların fiyatıyla gider. Varsayılan kapalıdır; aylık sınırı siz koyarsınız.'],
    ['Fatura keser mi?', 'Hayır. Vernuko tahsilat kaydı tutar; e-Arşiv veya e-Fatura için muhasebecinizin ya da fatura programınızın kullanılması gerekir.'],
    ['KVKK açısından ne değişiyor?', 'Kayıtlar sizin bilgisayarınızda durduğu için veri sorumlusu sizsiniz ve veriler bir yazılım firmasının sunucusuna gönderilmez. Program kişi bazında dışa aktarma ve anonimleştirme araçları sunar. Kendi aydınlatma metninizi hazırlamanız gerekir.'],
    ['Deneme bitince kayıtlarım ne olur?', 'Hiçbir şey silinmez. Program salt okunur olur; lisans anahtarını girdiğinizde aynı kayıtlarla devam edersiniz.'],
];
?>
<!-- wp:group {"tagName":"section","anchor":"sorular","align":"full","style":{"spacing":{"padding":{"top":"var:preset|spacing|60","bottom":"var:preset|spacing|60"}},"border":{"top":{"color":"var:preset|color|cizgi","width":"1px"}}},"layout":{"type":"constrained"}} -->
<section id="sorular" class="wp-block-group alignfull" style="border-top-color:var(--wp--preset--color--cizgi);border-top-width:1px;padding-top:var(--wp--preset--spacing--60);padding-bottom:var(--wp--preset--spacing--60)"><!-- wp:heading -->
<h2 class="wp-block-heading">Sorular</h2>
<!-- /wp:heading -->

<?php foreach ($sorular as [$soru, $cevap]) : ?>
<!-- wp:details -->
<details class="wp-block-details"><summary><?php echo esc_html($soru); ?></summary><!-- wp:paragraph -->
<p><?php echo esc_html($cevap); ?></p>
<!-- /wp:paragraph --></details>
<!-- /wp:details -->

<?php endforeach; ?></section>
<!-- /wp:group -->
