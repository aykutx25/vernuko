<?php
/**
 * Title: Üst bölüm
 * Slug: vernuko/ust
 * Categories: vernuko
 * Inserter: false
 */
?>
<!-- wp:group {"tagName":"header","className":"vk-ust","layout":{"type":"constrained"}} -->
<header class="wp-block-group vk-ust"><!-- wp:group {"align":"wide","style":{"spacing":{"padding":{"top":"0.9rem","bottom":"0.9rem"},"blockGap":"1.5rem"}},"layout":{"type":"flex","flexWrap":"nowrap","justifyContent":"space-between"}} -->
<div class="wp-block-group alignwide" style="padding-top:0.9rem;padding-bottom:0.9rem"><!-- wp:html -->
<a class="vk-logo" href="<?php echo esc_url(home_url('/')); ?>" aria-label="Vernuko, ana sayfa"><img src="<?php echo esc_url(get_template_directory_uri() . '/assets/img/vernuko.png'); ?>" alt="" width="34" height="34"><span>Vernuko</span></a>
<!-- /wp:html -->

<!-- wp:group {"style":{"spacing":{"blockGap":"1.75rem"}},"layout":{"type":"flex","flexWrap":"nowrap","justifyContent":"right"}} -->
<div class="wp-block-group"><!-- wp:navigation {"overlayMenu":"mobile","icon":"menu","overlayBackgroundColor":"defter","overlayTextColor":"murekkep","layout":{"type":"flex","justifyContent":"right"},"style":{"spacing":{"blockGap":"1.5rem"}}} -->
<!-- wp:navigation-link {"label":"Programlar","url":"<?php echo esc_url(home_url('/#programlar')); ?>","kind":"custom","isTopLevelLink":true} /-->
<!-- wp:navigation-link {"label":"İşletmeler","url":"<?php echo esc_url(home_url('/#isletmeler')); ?>","kind":"custom","isTopLevelLink":true} /-->
<!-- wp:navigation-link {"label":"Fiyat","url":"<?php echo esc_url(home_url('/#fiyat')); ?>","kind":"custom","isTopLevelLink":true} /-->
<!-- wp:navigation-link {"label":"Sorular","url":"<?php echo esc_url(home_url('/#sorular')); ?>","kind":"custom","isTopLevelLink":true} /-->
<!-- wp:navigation-link {"label":"Lisanslarım","url":"<?php echo esc_url(home_url('/lisanslarim/')); ?>","kind":"custom","isTopLevelLink":true} /-->
<!-- wp:navigation-link {"label":"Deneme sürümünü indir","url":"<?php echo esc_url(home_url('/indir/')); ?>","kind":"custom","isTopLevelLink":true,"className":"vk-menu-indir"} /-->
<!-- /wp:navigation -->

<!-- wp:buttons {"className":"vk-ust__dugme"} -->
<div class="wp-block-buttons vk-ust__dugme"><!-- wp:button {"style":{"spacing":{"padding":{"top":"0.7rem","bottom":"0.7rem","left":"1.1rem","right":"1.1rem"}},"typography":{"fontSize":"0.9375rem"}}} -->
<div class="wp-block-button has-custom-font-size" style="font-size:0.9375rem"><a class="wp-block-button__link wp-element-button" href="<?php echo esc_url(home_url('/indir/')); ?>" style="padding-top:0.7rem;padding-right:1.1rem;padding-bottom:0.7rem;padding-left:1.1rem">Deneme sürümünü indir</a></div>
<!-- /wp:button --></div>
<!-- /wp:buttons --></div>
<!-- /wp:group --></div>
<!-- /wp:group --></header>
<!-- /wp:group -->
