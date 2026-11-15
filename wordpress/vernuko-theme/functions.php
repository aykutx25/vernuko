<?php
/**
 * Vernuko satış teması.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

defined('ABSPATH') || exit;

require_once __DIR__ . '/inc/urunler.php';
require_once __DIR__ . '/inc/pano.php';
require_once __DIR__ . '/inc/fis.php';

const VERNUKO_TEMA_SURUM = '1.3.0';

add_action('after_setup_theme', static function (): void {
    add_theme_support('editor-styles');
    add_editor_style('assets/css/site.css');
    add_theme_support('responsive-embeds');
});

add_action('init', static function (): void {
    wp_register_script('vernuko-bloklar', get_template_directory_uri() . '/assets/js/bloklar.js', ['wp-blocks', 'wp-element', 'wp-server-side-render', 'wp-block-editor', 'wp-components'], (string) filemtime(get_template_directory() . '/assets/js/bloklar.js'), true);
}, 5);

add_action('wp_enqueue_scripts', static function (): void {
    $dir = get_template_directory();
    $uri = get_template_directory_uri();
    wp_enqueue_style('vernuko', $uri . '/assets/css/site.css', [], (string) filemtime($dir . '/assets/css/site.css'));
    wp_register_script('vernuko-pano', $uri . '/assets/js/pano.js', [], (string) filemtime($dir . '/assets/js/pano.js'), ['strategy' => 'defer', 'in_footer' => true]);
    wp_register_script('vernuko-fis', $uri . '/assets/js/fis.js', [], (string) filemtime($dir . '/assets/js/fis.js'), ['strategy' => 'defer', 'in_footer' => true]);
});

add_action('init', static function (): void {
    add_shortcode('vernuko_pano', 'vernuko_pano_shortcode');
    add_shortcode('vernuko_fis', 'vernuko_fis_shortcode');
    add_shortcode('vernuko_urunler', static fn ($a = []) => vernuko_urunler_html((string) (is_array($a) ? ($a['haric'] ?? '') : '')));
    // The same as blocks: templates expand patterns after shortcodes, so a shortcode inside a template pattern
    // would print as text. Server-rendered; the editor shows a live preview and a program picker (assets/js/bloklar.js).
    $urun = ['type' => 'string', 'default' => VERNUKO_RANDEVU];
    register_block_type('vernuko/pano', [
        'api_version' => 3,
        'title' => 'Vernuko gün ekranı',
        'category' => 'widgets',
        'attributes' => ['ekran' => ['type' => 'string', 'default' => 'randevu']],
        'render_callback' => static fn (array $a = []): string => vernuko_pano_shortcode(['ekran' => (string) ($a['ekran'] ?? 'randevu')]),
        'editor_script_handles' => ['vernuko-bloklar'],
        'supports' => ['html' => false, 'align' => ['wide', 'full']],
    ]);
    register_block_type('vernuko/fis', [
        'api_version' => 3,
        'title' => 'Vernuko fiyat fişi',
        'category' => 'widgets',
        'attributes' => ['urun' => $urun],
        'render_callback' => static fn (array $a = []): string => vernuko_fis_shortcode(['urun' => (string) ($a['urun'] ?? VERNUKO_RANDEVU)]),
        'editor_script_handles' => ['vernuko-bloklar'],
        'supports' => ['html' => false],
    ]);
    register_block_type('vernuko/urunler', [
        'api_version' => 3,
        'title' => 'Vernuko programları',
        'category' => 'widgets',
        'attributes' => ['haric' => ['type' => 'string', 'default' => '']],
        'render_callback' => static fn (array $a = []): string => vernuko_blok_sar(vernuko_urunler_html((string) ($a['haric'] ?? ''))),
        'editor_script_handles' => ['vernuko-bloklar'],
        'supports' => ['html' => false, 'align' => ['wide']],
    ]);
    register_block_type('vernuko/urun-logo', [
        'api_version' => 3,
        'title' => 'Vernuko program logosu',
        'category' => 'widgets',
        'attributes' => ['urun' => $urun],
        'render_callback' => static fn (array $a = []): string => '<p ' . get_block_wrapper_attributes(['class' => 'vk-urun-logo-blok']) . '>' . vernuko_urun_logosu((string) ($a['urun'] ?? VERNUKO_RANDEVU)) . '</p>',
        'editor_script_handles' => ['vernuko-bloklar'],
        'supports' => ['html' => false, 'align' => ['wide']],
    ]);
    register_block_type('vernuko/urun-satis', [
        'api_version' => 3,
        'title' => 'Vernuko program satışı (fiyat ve indirme ya da "geliştiriliyor")',
        'category' => 'widgets',
        'attributes' => ['urun' => $urun],
        'render_callback' => static fn (array $a = []): string => vernuko_blok_sar(vernuko_urun_satis_html((string) ($a['urun'] ?? VERNUKO_RANDEVU))),
        'editor_script_handles' => ['vernuko-bloklar'],
        'supports' => ['html' => false, 'align' => ['wide']],
    ]);
    register_block_pattern_category('vernuko', ['label' => 'Vernuko']);
    register_block_style('core/button', ['name' => 'vk-metin', 'label' => 'Metin bağlantısı']);
});

/** A block's wrapper (alignment and the editor's own classes) around a server-rendered part; nothing when it is empty. */
function vernuko_blok_sar(string $html): string
{
    return $html === '' ? '' : '<div ' . get_block_wrapper_attributes() . '>' . $html . '</div>';
}

/*
 * The site keeps the promise the program makes: nothing about the visitor goes to a third party.
 * No emoji script from s.w.org, no Gravatar images, no DNS prefetch to WordPress.org. (Fonts are local: theme.json.)
 */
add_action('init', static function (): void {
    remove_action('wp_head', 'print_emoji_detection_script', 7);
    remove_action('admin_print_scripts', 'print_emoji_detection_script');
    remove_action('wp_print_styles', 'print_emoji_styles');
    remove_action('admin_print_styles', 'print_emoji_styles');
    remove_filter('the_content_feed', 'wp_staticize_emoji');
    remove_filter('comment_text_rss', 'wp_staticize_emoji');
    remove_filter('wp_mail', 'wp_staticize_emoji_for_email');
});
add_filter('emoji_svg_url', '__return_false');
add_filter('wp_resource_hints', static function (array $urls, string $relation): array {
    return $relation === 'dns-prefetch' ? array_values(array_filter($urls, static fn ($u) => !str_contains(is_array($u) ? (string) ($u['href'] ?? '') : (string) $u, 's.w.org'))) : $urls;
}, 10, 2);
add_filter('pre_option_show_avatars', '__return_zero');

/** The site's icon: the leaf mark (white in a dark browser tab). A Site Icon set in WordPress takes precedence. */
add_action('wp_head', static function (): void {
    if (function_exists('has_site_icon') && has_site_icon()) {
        return;
    }
    $img = get_template_directory_uri() . '/assets/img/';
    echo '<link rel="icon" href="' . esc_url($img . 'favicon.svg') . '" type="image/svg+xml">' . "\n";
    echo '<link rel="apple-touch-icon" href="' . esc_url($img . 'apple-touch-icon.png') . '">' . "\n";
}, 2);

/** A small preload so the headline does not jump when the font arrives. */
add_action('wp_head', static function (): void {
    echo '<link rel="preload" href="' . esc_url(get_template_directory_uri() . '/assets/fonts/schibsted-grotesk.woff2') . '" as="font" type="font/woff2" crossorigin>' . "\n";
}, 1);
