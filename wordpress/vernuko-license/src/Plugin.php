<?php
/**
 * WordPress / WooCommerce katmanı. Ayarlar wp-config.php'deki sabitlerdedir (veritabanında sır tutulmaz):
 *   VERNUKO_LICENSE_SIGNING_KEY   özel imzalama anahtarı (PEM, ECDSA P-256)
 *   VERNUKO_LICENSE_KEY_ID        anahtar kimliği (ör. "2026-11"); masaüstü açık anahtarı bununla bulur
 *   VERNUKO_LICENSE_KEY_SECRET    lisans anahtarlarını türetme sırrı (base64, 32 bayt)
 *   VERNUKO_LICENSE_RELEASES_DIR  (isteğe bağlı) güncelleme dosyaları klasörü, web kökünün DIŞINDA
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

namespace Vernuko\License;

final class Plugin
{
    public const REST_NAMESPACE = 'vernuko/v1';
    public const ACCOUNT_ENDPOINT = 'vernuko-lisanslar';
    private const CAP = 'manage_woocommerce';

    private static ?Issuer $issuer = null;
    private static string $file = '';

    public static function boot(string $file): void
    {
        self::$file = $file;
        add_action('rest_api_init', [self::class, 'registerRoutes']);
        add_action('init', [self::class, 'registerEndpoint']);
        add_action('admin_menu', [self::class, 'adminMenu']);
        add_action('admin_post_vernuko_license', [self::class, 'adminAction']);
        add_action('admin_post_vernuko_device', [self::class, 'customerRemoveDevice']);
        add_action('before_woocommerce_init', static function (): void {
            if (class_exists(\Automattic\WooCommerce\Utilities\FeaturesUtil::class)) {
                \Automattic\WooCommerce\Utilities\FeaturesUtil::declare_compatibility('custom_order_tables', self::$file, true);
            }
        });

        // WooCommerce: product settings, sale → license, refund → cancel, keys in e-mails and pages.
        add_action('woocommerce_product_options_general_product_data', [self::class, 'productFields']);
        add_action('woocommerce_process_product_meta', [self::class, 'saveProductFields']);
        // Status hooks fire before WooCommerce sends the "order received/completed" e-mails, so the key is in them.
        add_action('woocommerce_order_status_processing', [self::class, 'grant'], 5);
        add_action('woocommerce_order_status_completed', [self::class, 'grant'], 5);
        add_action('woocommerce_payment_complete', [self::class, 'grant'], 5);
        add_action('woocommerce_order_status_refunded', [self::class, 'revoke']);
        add_action('woocommerce_order_status_cancelled', [self::class, 'revoke']);
        add_action('woocommerce_order_details_after_order_table', [self::class, 'orderKeys']);
        add_action('woocommerce_email_after_order_table', [self::class, 'emailKeys'], 10, 4);
        add_filter('woocommerce_add_to_cart_validation', [self::class, 'validateRenewal'], 10, 2);
        add_filter('woocommerce_add_cart_item_data', [self::class, 'cartItemData'], 10, 2);
        add_filter('woocommerce_get_item_data', [self::class, 'showCartItemData'], 10, 2);
        add_action('woocommerce_checkout_create_order_line_item', [self::class, 'orderItemData'], 10, 3);
        add_filter('woocommerce_account_menu_items', [self::class, 'accountMenu']);
        add_action('woocommerce_account_' . self::ACCOUNT_ENDPOINT . '_endpoint', [self::class, 'accountPage']);
    }

    public static function activate(): void
    {
        global $wpdb;
        require_once ABSPATH . 'wp-admin/includes/upgrade.php';
        foreach (Schema::mysql($wpdb->prefix, $wpdb->get_charset_collate()) as $sql) {
            dbDelta($sql);
        }
        self::registerEndpoint();
        flush_rewrite_rules();
    }

    /** Null when the wp-config constants are missing or invalid (endpoints then answer 503). */
    public static function issuer(): ?Issuer
    {
        if (self::$issuer !== null) {
            return self::$issuer;
        }
        if (!defined('VERNUKO_LICENSE_SIGNING_KEY') || !defined('VERNUKO_LICENSE_KEY_ID') || !defined('VERNUKO_LICENSE_KEY_SECRET')) {
            return null;
        }
        $secret = base64_decode((string) VERNUKO_LICENSE_KEY_SECRET, true);
        if ($secret === false || strlen($secret) < 32) {
            return null;
        }
        try {
            $signer = Signer::fromPem((string) VERNUKO_LICENSE_SIGNING_KEY, (string) VERNUKO_LICENSE_KEY_ID);
        } catch (\InvalidArgumentException) {
            return null;
        }
        global $wpdb;
        return self::$issuer = new Issuer(new WpDb($wpdb), $signer, $secret);
    }

    // ---------------------------------------------------------------- REST (desktop app)

    public static function registerRoutes(): void
    {
        foreach (['activate', 'refresh', 'deactivate', 'trial', 'updates/permission'] as $action) {
            register_rest_route(self::REST_NAMESPACE, '/' . $action, [
                'methods' => 'POST',
                'permission_callback' => '__return_true',
                'callback' => static fn (\WP_REST_Request $r) => self::api('v1/' . $action, $r),
            ]);
        }
        register_rest_route(self::REST_NAMESPACE, '/updates/feed/(?P<file>[A-Za-z0-9._-]+)', [
            'methods' => 'GET',
            'permission_callback' => '__return_true',
            'callback' => [self::class, 'updateFeed'],
        ]);
    }

    private static function api(string $route, \WP_REST_Request $request): \WP_REST_Response
    {
        $issuer = self::issuer();
        if ($issuer === null) {
            return new \WP_REST_Response(['code' => 'server.not_configured', 'message' => 'Lisans sunucusu henüz yapılandırılmadı.'], 503);
        }
        if (!self::rateLimit()) {
            return new \WP_REST_Response(['code' => 'request.rate_limited', 'message' => 'Çok fazla deneme yapıldı. Bir dakika sonra tekrar deneyin.'], 429);
        }
        $body = $request->get_json_params();
        [$status, $data] = (new Api($issuer))->handle($route, is_array($body) ? $body : []);
        $response = new \WP_REST_Response($data, $status);
        $response->header('Cache-Control', 'no-store');
        return $response;
    }

    /** 30 requests per minute per IP (transients; the IP itself is not stored). */
    private static function rateLimit(): bool
    {
        $ip = (string) ($_SERVER['REMOTE_ADDR'] ?? '-');
        $key = 'vk_rl_' . md5($ip . '|' . gmdate('YmdHi'));
        $count = (int) get_transient($key);
        if ($count >= 30) {
            return false;
        }
        set_transient($key, $count + 1, 120);
        return true;
    }

    /** Velopack files only for an activated installation with a running subscription. */
    public static function updateFeed(\WP_REST_Request $request)
    {
        $issuer = self::issuer();
        if ($issuer === null || !defined('VERNUKO_LICENSE_RELEASES_DIR')) {
            return new \WP_REST_Response(['code' => 'updates.not_configured', 'message' => 'Güncelleme dosyaları bu sitede yayınlanmıyor.'], 404);
        }
        $permission = $issuer->updatePermission($request->get_header('x_vernuko_activation'), $request->get_header('x_vernuko_device'));
        if (!$permission['allowed']) {
            return new \WP_REST_Response(['code' => 'updates.not_allowed', 'message' => $permission['message']], 403);
        }
        $file = Api::safeReleaseFile((string) VERNUKO_LICENSE_RELEASES_DIR, (string) $request['file']);
        if ($file === null) {
            return new \WP_REST_Response(['code' => 'updates.not_found', 'message' => 'Dosya yok.'], 404);
        }
        while (ob_get_level() > 0) {
            ob_end_clean();
        }
        header('Content-Type: ' . (str_ends_with($file, '.json') ? 'application/json' : 'application/octet-stream'));
        header('Content-Length: ' . filesize($file));
        header('Cache-Control: no-store');
        readfile($file);
        exit;
    }

    // ---------------------------------------------------------------- WooCommerce: product settings

    public static function productFields(): void
    {
        echo '<div class="options_group">';
        woocommerce_wp_checkbox(['id' => '_vernuko_license', 'label' => 'Vernuko lisansı', 'description' => 'Ödeme tamamlanınca lisans anahtarı oluşturulur ve müşteriye e-postayla gönderilir.']);
        woocommerce_wp_select(['id' => '_vernuko_kind', 'label' => 'Tür', 'options' => ['new' => 'Yeni lisans', 'renewal' => 'Yenileme (Hesabım › Lisanslarım\'dan alınır)']]);
        woocommerce_wp_text_input(['id' => '_vernuko_edition', 'label' => 'Paket adı', 'placeholder' => 'Standart']);
        woocommerce_wp_text_input(['id' => '_vernuko_devices', 'label' => 'Bilgisayar sayısı', 'type' => 'number', 'placeholder' => '1', 'custom_attributes' => ['min' => '1', 'max' => '50'], 'description' => 'Ana bilgisayar + bağlı bilgisayarlar toplamı.']);
        woocommerce_wp_text_input(['id' => '_vernuko_days', 'label' => 'Süre (gün)', 'type' => 'number', 'placeholder' => '365', 'custom_attributes' => ['min' => '1']]);
        echo '</div>';
    }

    public static function saveProductFields(int $productId): void
    {
        update_post_meta($productId, '_vernuko_license', isset($_POST['_vernuko_license']) ? 'yes' : 'no');
        $kind = sanitize_text_field(wp_unslash($_POST['_vernuko_kind'] ?? 'new'));
        update_post_meta($productId, '_vernuko_kind', $kind === 'renewal' ? 'renewal' : 'new');
        update_post_meta($productId, '_vernuko_edition', sanitize_text_field(wp_unslash($_POST['_vernuko_edition'] ?? '')));
        update_post_meta($productId, '_vernuko_devices', max(1, min(50, absint($_POST['_vernuko_devices'] ?? 1))));
        update_post_meta($productId, '_vernuko_days', max(1, absint($_POST['_vernuko_days'] ?? 365)));
    }

    /** @return array{kind:string,edition:string,devices:int,days:int}|null */
    private static function plan(int $productId): ?array
    {
        $product = wc_get_product($productId);
        if (!$product) {
            return null;
        }
        $id = $product->get_parent_id() ?: $product->get_id();
        if (get_post_meta($id, '_vernuko_license', true) !== 'yes') {
            return null;
        }
        return [
            'kind' => get_post_meta($id, '_vernuko_kind', true) === 'renewal' ? 'renewal' : 'new',
            'edition' => (string) (get_post_meta($id, '_vernuko_edition', true) ?: 'Standart'),
            'devices' => max(1, (int) (get_post_meta($id, '_vernuko_devices', true) ?: 1)),
            'days' => max(1, (int) (get_post_meta($id, '_vernuko_days', true) ?: 365)),
        ];
    }

    // ---------------------------------------------------------------- WooCommerce: renewals in the cart

    public static function validateRenewal(bool $valid, int $productId): bool
    {
        $plan = self::plan($productId);
        if ($plan === null || $plan['kind'] !== 'renewal') {
            return $valid;
        }
        if (self::ownedLicense(sanitize_text_field(wp_unslash($_REQUEST['vernuko_renew'] ?? ''))) === null) {
            wc_add_notice('Yenileme, Hesabım › Lisanslarım sayfasındaki "Yenile" düğmesiyle sepete eklenir.', 'error');
            return false;
        }
        return $valid;
    }

    public static function cartItemData(array $data, int $productId): array
    {
        $plan = self::plan($productId);
        if ($plan !== null && $plan['kind'] === 'renewal') {
            $license = self::ownedLicense(sanitize_text_field(wp_unslash($_REQUEST['vernuko_renew'] ?? '')));
            if ($license !== null) {
                $data['vernuko_renew'] = $license['id'];
            }
        }
        return $data;
    }

    public static function showCartItemData(array $data, array $item): array
    {
        if (!empty($item['vernuko_renew']) && ($issuer = self::issuer()) !== null) {
            $data[] = ['key' => 'Yenilenen lisans', 'value' => esc_html(self::maskedKey($issuer->keyFor($item['vernuko_renew'])))];
        }
        return $data;
    }

    public static function orderItemData(\WC_Order_Item_Product $item, string $cartKey, array $values): void
    {
        if (!empty($values['vernuko_renew'])) {
            $item->add_meta_data('_vernuko_renew_license', $values['vernuko_renew'], true);
        }
    }

    // ---------------------------------------------------------------- WooCommerce: sale → license

    /** Paid (online) or marked completed (bank transfer): create/extend licenses once per order line. */
    public static function grant(int $orderId): void
    {
        $issuer = self::issuer();
        $order = wc_get_order($orderId);
        if ($issuer === null || !$order || !$order->is_paid()) {
            return;
        }
        $name = trim($order->get_billing_company() ?: ($order->get_billing_first_name() . ' ' . $order->get_billing_last_name())) ?: 'Müşteri';
        $email = $order->get_billing_email() ?: null;
        $userId = $order->get_customer_id() ?: null;
        foreach ($order->get_items() as $itemId => $item) {
            if (!$item instanceof \WC_Order_Item_Product) {
                continue;
            }
            $plan = self::plan($item->get_variation_id() ?: $item->get_product_id());
            if ($plan === null) {
                continue;
            }
            try {
                $note = $issuer->grantOrderItem($orderId, (int) $itemId, $plan['kind'], $plan['edition'], $plan['devices'], $plan['days'],
                    (int) $item->get_quantity(), (string) $item->get_meta('_vernuko_renew_license') ?: null, $name, $email, $userId);
                if ($note !== null) {
                    $order->add_order_note($note);
                }
            } catch (\Throwable $e) {
                $order->add_order_note('Vernuko: lisans oluşturulamadı — ' . $e->getMessage());
            }
        }
    }

    /** Refunded/cancelled order: new licenses end now; renewal days are taken back (never before today). */
    public static function revoke(int $orderId): void
    {
        $issuer = self::issuer();
        $order = wc_get_order($orderId);
        if ($issuer === null || !$order) {
            return;
        }
        try {
            if ($issuer->revokeOrder($orderId, "Sipariş #$orderId iade/iptal") > 0) {
                $order->add_order_note('Vernuko: sipariş iade/iptal edildiği için lisans durduruldu. Müşterinin verileri silinmez; program salt-okunur moda geçer.');
            }
        } catch (\Throwable $e) {
            $order->add_order_note('Vernuko: lisans durdurulamadı — ' . $e->getMessage());
        }
    }

    // ---------------------------------------------------------------- Keys for the customer

    /** @return string[] full keys of the new licenses in this order */
    private static function orderLicenseKeys(\WC_Order $order): array
    {
        $issuer = self::issuer();
        if ($issuer === null || $order->has_status(['refunded', 'cancelled', 'failed'])) {
            return [];
        }
        return array_map(static fn (array $l) => $issuer->keyFor($l['id']), $issuer->orderLicenses($order->get_id()));
    }

    public static function orderKeys(\WC_Order $order): void
    {
        $keys = self::orderLicenseKeys($order);
        if (!$keys) {
            return;
        }
        echo '<section class="vernuko-license-keys"><h2>Vernuko lisans anahtarınız</h2>';
        foreach ($keys as $key) {
            echo '<p><code style="font-size:1.2em">' . esc_html($key) . '</code></p>';
        }
        echo '<p>Vernuko\'da Ayarlar › Lisans › "Lisans anahtarı" alanına girip Etkinleştir\'e basın. Anahtarlarınızı her zaman Hesabım › Lisanslarım sayfasında görebilirsiniz.</p></section>';
    }

    public static function emailKeys($order, bool $sentToAdmin, bool $plainText, $email = null): void
    {
        if ($sentToAdmin || !$order instanceof \WC_Order) {
            return;
        }
        $keys = self::orderLicenseKeys($order);
        if (!$keys) {
            return;
        }
        if ($plainText) {
            echo "\nVernuko lisans anahtarınız:\n" . implode("\n", $keys) . "\nVernuko'da Ayarlar › Lisans › Lisans anahtarı alanına girip Etkinleştir'e basın.\n\n";
            return;
        }
        echo '<h2>Vernuko lisans anahtarınız</h2>';
        foreach ($keys as $key) {
            echo '<p style="font-family:Consolas,monospace;font-size:18px"><strong>' . esc_html($key) . '</strong></p>';
        }
        echo '<p>Vernuko\'da Ayarlar › Lisans › "Lisans anahtarı" alanına girip Etkinleştir\'e basın.</p>';
    }

    // ---------------------------------------------------------------- My Account › Lisanslarım

    public static function registerEndpoint(): void
    {
        add_rewrite_endpoint(self::ACCOUNT_ENDPOINT, EP_ROOT | EP_PAGES);
    }

    public static function accountMenu(array $items): array
    {
        $out = [];
        foreach ($items as $key => $label) {
            if ($key === 'customer-logout') {
                $out[self::ACCOUNT_ENDPOINT] = 'Lisanslarım';
            }
            $out[$key] = $label;
        }
        return isset($out[self::ACCOUNT_ENDPOINT]) ? $out : $out + [self::ACCOUNT_ENDPOINT => 'Lisanslarım'];
    }

    public static function accountPage(): void
    {
        $issuer = self::issuer();
        $user = wp_get_current_user();
        if ($issuer === null || !$user->exists()) {
            echo '<p>Lisanslar şu anda gösterilemiyor.</p>';
            return;
        }
        $licenses = $issuer->licenses(null, $user->ID, 100, $user->user_email);
        if (!$licenses) {
            echo '<p>Henüz lisansınız yok.</p>';
            return;
        }
        if (isset($_GET['vernuko_removed'])) {
            echo '<div class="woocommerce-message">Bilgisayar kaldırıldı; lisansı yeni bilgisayarda etkinleştirebilirsiniz.</div>';
        }
        $renewal = self::renewalProductId();
        foreach ($licenses as $l) {
            $end = $issuer->effectiveEnd($l);
            $active = $end > time();
            echo '<div class="vernuko-license" style="border:1px solid #ddd;border-radius:6px;padding:14px;margin:0 0 16px">';
            echo '<p><code style="font-size:1.15em">' . esc_html($issuer->keyFor($l['id'])) . '</code></p>';
            echo '<p>' . esc_html($l['edition']) . ' · ' . (int) $l['max_devices'] . ' bilgisayar · ' . ($active ? 'Geçerlilik: ' . esc_html(wp_date('d.m.Y', (int) $l['paid_until'])) : '<strong>Süresi doldu</strong> (' . esc_html(wp_date('d.m.Y', (int) $l['paid_until'])) . ')') . ($l['status'] === 'cancelled' ? ' · İptal edildi' : '') . '</p>';
            $devices = array_filter($issuer->activations($l['id']), static fn ($a) => $a['deactivated_at'] === null);
            if ($devices) {
                echo '<p><strong>Etkin bilgisayarlar</strong></p><ul>';
                foreach ($devices as $a) {
                    $url = wp_nonce_url(admin_url('admin-post.php?action=vernuko_device&activation=' . rawurlencode($a['id'])), 'vernuko_device_' . $a['id']);
                    echo '<li>' . esc_html($a['machine_label'] ?: 'Bilgisayar') . ' — son görülme ' . esc_html(wp_date('d.m.Y', (int) $a['last_seen'])) . ' · <a href="' . esc_url($url) . '" onclick="return confirm(\'Bu bilgisayardaki lisans kaldırılsın mı? Oradaki veriler silinmez; program salt-okunur olur.\')">Kaldır</a></li>';
                }
                echo '</ul><p style="font-size:.9em">Bilgisayar değiştirirken önce eski bilgisayarda Vernuko\'da Ayarlar › Lisans › "Bu bilgisayardan kaldır" yapın. Eski bilgisayar bozulduysa buradan kaldırabilirsiniz.</p>';
            }
            if ($renewal !== null && $l['status'] !== 'cancelled') {
                $url = add_query_arg(['add-to-cart' => $renewal, 'vernuko_renew' => $l['id']], wc_get_cart_url());
                echo '<p><a class="button" href="' . esc_url($url) . '">Yenile</a></p>';
            }
            echo '</div>';
        }
    }

    /** Customer removes a broken/sold computer from their own license (nonce + ownership checked). */
    public static function customerRemoveDevice(): void
    {
        $activationId = sanitize_text_field(wp_unslash($_GET['activation'] ?? ''));
        check_admin_referer('vernuko_device_' . $activationId);
        $issuer = self::issuer();
        $user = wp_get_current_user();
        if ($issuer === null || !$user->exists()) {
            wp_die('Giriş yapmanız gerekiyor.', 403);
        }
        foreach ($issuer->licenses(null, $user->ID, 100, $user->user_email) as $l) {
            foreach ($issuer->activations($l['id']) as $a) {
                if ($a['id'] === $activationId) {
                    $issuer->removeActivation($activationId, 'müşteri, Hesabım');
                    wp_safe_redirect(add_query_arg('vernuko_removed', '1', wc_get_account_endpoint_url(self::ACCOUNT_ENDPOINT)));
                    exit;
                }
            }
        }
        wp_die('Bu bilgisayar size ait bir lisansta değil.', 403);
    }

    private static function renewalProductId(): ?int
    {
        $ids = get_posts(['post_type' => 'product', 'post_status' => 'publish', 'numberposts' => 1, 'fields' => 'ids',
            'meta_query' => [['key' => '_vernuko_license', 'value' => 'yes'], ['key' => '_vernuko_kind', 'value' => 'renewal']]]);
        return $ids ? (int) $ids[0] : null;
    }

    private static function ownedLicense(string $licenseId): ?array
    {
        $issuer = self::issuer();
        $user = wp_get_current_user();
        if ($issuer === null || $licenseId === '' || !$user->exists()) {
            return null;
        }
        foreach ($issuer->licenses(null, $user->ID, 100, $user->user_email) as $l) {
            if ($l['id'] === $licenseId) {
                return $l;
            }
        }
        return null;
    }

    private static function maskedKey(string $key): string
    {
        return 'VRNK-…-' . substr($key, -5);
    }

    // ---------------------------------------------------------------- Admin

    public static function adminMenu(): void
    {
        $cap = current_user_can(self::CAP) ? self::CAP : 'manage_options';
        add_menu_page('Vernuko Lisanslar', 'Vernuko Lisans', $cap, 'vernuko-license', [self::class, 'adminPage'], 'dashicons-admin-network', 56);
    }

    public static function adminPage(): void
    {
        if (!current_user_can(self::CAP) && !current_user_can('manage_options')) {
            wp_die('Yetkiniz yok.', 403);
        }
        $issuer = self::issuer();
        echo '<div class="wrap"><h1>Vernuko Lisanslar</h1>';
        if ($notice = get_transient('vernuko_admin_notice_' . get_current_user_id())) {
            delete_transient('vernuko_admin_notice_' . get_current_user_id());
            echo '<div class="notice notice-info"><p>' . wp_kses_post($notice) . '</p></div>';
        }
        if ($issuer === null) {
            self::setupPanel();
            echo '</div>';
            return;
        }

        echo '<h2>Masaüstü uygulamaya girecek değerler</h2><table class="widefat striped" style="max-width:900px"><tbody>';
        echo '<tr><th>VERNUKO_LICENSE_SERVER_URL</th><td><code>' . esc_html(rest_url('vernuko/')) . '</code></td></tr>';
        echo '<tr><th>VERNUKO_LICENSE_KEYS</th><td><code style="word-break:break-all">' . esc_html(VERNUKO_LICENSE_KEY_ID . ':' . Signer::fromPem((string) VERNUKO_LICENSE_SIGNING_KEY, (string) VERNUKO_LICENSE_KEY_ID)->publicKeyBase64()) . '</code></td></tr>';
        echo '</tbody></table><p>Bunları GitHub deposunda Settings › Secrets and variables › Actions › <strong>Variables</strong> bölümüne girin (gizli değiller). Sonraki sürüm bu siteden lisans alır.</p>';

        $action = esc_url(admin_url('admin-post.php'));
        $nonce = wp_nonce_field('vernuko_license', '_wpnonce', true, false);
        echo '<h2>Elle lisans oluştur</h2><p>Havale/EFT veya mağaza dışı satışlar için. WooCommerce siparişleri lisansı kendisi oluşturur.</p>';
        echo '<form method="post" action="' . $action . '">' . $nonce . '<input type="hidden" name="action" value="vernuko_license"><input type="hidden" name="op" value="create">';
        echo '<input name="name" placeholder="İşletme / müşteri adı" required> <input name="email" type="email" placeholder="E-posta"> <input name="edition" placeholder="Standart"> ';
        echo '<input name="devices" type="number" min="1" max="50" value="1" style="width:70px" title="Bilgisayar"> <input name="days" type="number" min="1" value="365" style="width:80px" title="Gün"> ';
        echo '<input name="note" placeholder="Not"> <button class="button button-primary">Oluştur</button></form>';

        $search = sanitize_text_field(wp_unslash($_GET['s'] ?? ''));
        echo '<h2>Lisanslar</h2><form method="get"><input type="hidden" name="page" value="vernuko-license"><input name="s" value="' . esc_attr($search) . '" placeholder="Ad, e-posta veya lic_…"> <button class="button">Ara</button></form>';
        echo '<table class="widefat striped" style="margin-top:8px"><thead><tr><th>Müşteri</th><th>Paket</th><th>Bilgisayar</th><th>Dönem sonu</th><th>Durum</th><th>Kaynak</th><th></th></tr></thead><tbody>';
        foreach ($issuer->licenses($search ?: null) as $l) {
            $devices = array_values(array_filter($issuer->activations($l['id']), static fn ($a) => $a['deactivated_at'] === null));
            $source = $l['source'] === 'woocommerce' && $l['order_id'] ? '<a href="' . esc_url(admin_url('post.php?post=' . (int) $l['order_id'] . '&action=edit')) . '">Sipariş #' . (int) $l['order_id'] . '</a>' : esc_html($l['source']);
            echo '<tr><td><strong>' . esc_html($l['customer_name']) . '</strong><br><small>' . esc_html((string) $l['customer_email']) . ' · ' . esc_html($l['id']) . '</small></td>';
            echo '<td>' . esc_html($l['edition']) . '</td><td>' . count($devices) . ' / ' . (int) $l['max_devices'];
            foreach ($devices as $a) {
                echo '<br><small>' . esc_html($a['machine_label'] ?: $a['id']) . ' ' . self::adminButton('remove_device', $l['id'], 'Kaldır', $a['id']) . '</small>';
            }
            echo '</td><td>' . esc_html(wp_date('d.m.Y', (int) $l['paid_until'])) . '</td><td>' . esc_html($l['status'] === 'active' ? 'Etkin' : 'İptal') . '</td><td>' . $source . '</td>';
            echo '<td>' . self::adminButton('show_key', $l['id'], 'Anahtarı göster') . ' ' . self::adminButton('extend', $l['id'], '+365 gün') . ' ' . ($l['status'] === 'active' ? self::adminButton('cancel', $l['id'], 'İptal') : '') . '</td></tr>';
        }
        echo '</tbody></table></div>';
    }

    private static function adminButton(string $op, string $licenseId, string $label, string $activation = ''): string
    {
        $confirm = in_array($op, ['cancel', 'remove_device'], true) ? ' onclick="return confirm(\'Emin misiniz?\')"' : '';
        return '<form method="post" action="' . esc_url(admin_url('admin-post.php')) . '" style="display:inline">' . wp_nonce_field('vernuko_license', '_wpnonce', true, false)
            . '<input type="hidden" name="action" value="vernuko_license"><input type="hidden" name="op" value="' . esc_attr($op) . '"><input type="hidden" name="license" value="' . esc_attr($licenseId) . '"><input type="hidden" name="activation" value="' . esc_attr($activation) . '">'
            . '<button class="button button-small"' . $confirm . '>' . esc_html($label) . '</button></form>';
    }

    public static function adminAction(): void
    {
        check_admin_referer('vernuko_license');
        if (!current_user_can(self::CAP) && !current_user_can('manage_options')) {
            wp_die('Yetkiniz yok.', 403);
        }
        $op = sanitize_key($_POST['op'] ?? '');
        $issuer = self::issuer();
        $notice = '';
        try {
            if ($op === 'generate_keys') {
                [$pem, $public] = Signer::generate();
                $kid = gmdate('Y-m');
                $secret = base64_encode(random_bytes(32));
                $notice = '<strong>Anahtarlar oluşturuldu — yalnızca bir kez gösterilir.</strong> Aşağıdaki satırları <code>wp-config.php</code> dosyasına, "That\'s all, stop editing" satırının üstüne ekleyin ve bir kopyasını güvenli bir yerde (parola yöneticisi) saklayın. Bu anahtarlar kaybolursa satılmış lisanslar yenilenemez.'
                    . '<textarea readonly rows="12" style="width:100%;font-family:monospace">' . esc_textarea("define('VERNUKO_LICENSE_KEY_ID', '$kid');\ndefine('VERNUKO_LICENSE_KEY_SECRET', '$secret');\ndefine('VERNUKO_LICENSE_SIGNING_KEY', '" . trim($pem) . "');\n") . '</textarea>';
            } elseif ($issuer === null) {
                $notice = 'Önce anahtarları wp-config.php\'ye ekleyin.';
            } elseif ($op === 'create') {
                $created = $issuer->createLicense(sanitize_text_field(wp_unslash($_POST['name'] ?? '')), sanitize_email(wp_unslash($_POST['email'] ?? '')) ?: null, null,
                    sanitize_text_field(wp_unslash($_POST['edition'] ?? '')) ?: 'Standart', absint($_POST['devices'] ?? 1), absint($_POST['days'] ?? 365), 'manual', null, [],
                    sanitize_text_field(wp_unslash($_POST['note'] ?? '')) ?: null);
                $notice = 'Lisans oluşturuldu: <code style="font-size:1.2em">' . esc_html($created['key']) . '</code> — müşteriye iletin.';
            } else {
                $licenseId = sanitize_text_field(wp_unslash($_POST['license'] ?? ''));
                $user = wp_get_current_user()->user_login;
                switch ($op) {
                    case 'show_key':
                        $issuer->audit('license.key_viewed', $licenseId, "Anahtar gösterildi ($user)");
                        $notice = 'Anahtar: <code style="font-size:1.2em">' . esc_html($issuer->keyFor($licenseId)) . '</code>';
                        break;
                    case 'extend':
                        $issuer->extend($licenseId, 365, "Yönetici ($user)");
                        $notice = 'Lisans 365 gün uzatıldı.';
                        break;
                    case 'cancel':
                        $issuer->cancel($licenseId, false, "Yönetici ($user)");
                        $notice = 'Lisans iptal edildi; ödenmiş dönem sonuna kadar çalışır.';
                        break;
                    case 'remove_device':
                        $issuer->removeActivation(sanitize_text_field(wp_unslash($_POST['activation'] ?? '')), "yönetici $user");
                        $notice = 'Bilgisayar kaldırıldı.';
                        break;
                }
            }
        } catch (Error $e) {
            $notice = esc_html($e->getMessage());
        }
        set_transient('vernuko_admin_notice_' . get_current_user_id(), $notice, 300);
        wp_safe_redirect(admin_url('admin.php?page=vernuko-license'));
        exit;
    }

    private static function setupPanel(): void
    {
        echo '<div class="notice notice-warning"><p><strong>Lisans sunucusu henüz yapılandırılmadı.</strong> Masaüstü uygulama şu an bu siteden lisans alamaz.</p></div>';
        echo '<ol><li>Aşağıdaki düğmeyle anahtarları oluşturun (sunucuda saklanmaz, yalnızca size gösterilir).</li><li>Gösterilen satırları <code>wp-config.php</code> dosyasına ekleyin.</li><li>Bu sayfayı yenileyin: masaüstü uygulamaya girecek adres ve açık anahtar burada görünür.</li><li>WooCommerce\'te bir ürün açıp Genel sekmesinde "Vernuko lisansı" kutusunu işaretleyin.</li></ol>';
        echo '<form method="post" action="' . esc_url(admin_url('admin-post.php')) . '">' . wp_nonce_field('vernuko_license', '_wpnonce', true, false) . '<input type="hidden" name="action" value="vernuko_license"><input type="hidden" name="op" value="generate_keys"><button class="button button-primary">Anahtarları oluştur</button></form>';
        echo '<p>Not: Permalink ayarı "Düz" (Plain) ise REST adresleri çalışmaz; Ayarlar › Kalıcı Bağlantılar\'da "Yazı adı" seçin.</p>';
    }
}
