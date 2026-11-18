<?php
/**
 * Title: Ödeme sayfası üst bölümü
 * Slug: vernuko/odeme-ust
 * Categories: vernuko
 * Inserter: false
 */
$vk_cart = function_exists('wc_get_cart_url') ? wc_get_cart_url() : home_url('/');
?>
<!-- wp:group {"tagName":"header","className":"vk-ust vk-ust--odeme","layout":{"type":"constrained"}} -->
<header class="wp-block-group vk-ust vk-ust--odeme"><!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"0.9rem","bottom":"0.9rem"},"blockGap":"1.5rem"}},"layout":{"type":"flex","flexWrap":"nowrap","justifyContent":"space-between"}} -->
<div class="wp-block-group alignwide" style="padding-top:0.9rem;padding-bottom:0.9rem"><!-- wp:html -->
<a class="vk-logo" href="<?php echo esc_url(home_url('/')); ?>"><img src="<?php echo esc_url(get_template_directory_uri() . '/assets/img/vernuko-logo.svg'); ?>" alt="Vernuko, ana sayfa" width="174" height="30"></a>
<!-- /wp:html -->

<!-- wp:paragraph {"style":{"typography":{"fontSize":"0.9375rem"}}} -->
<p style="font-size:0.9375rem"><a href="<?php echo esc_url($vk_cart); ?>">Sepete dön</a></p>
<!-- /wp:paragraph --></div>
<!-- /wp:group --></header>
<!-- /wp:group -->
