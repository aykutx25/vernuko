<?php
/**
 * Kendi satış sistemi (WooCommerce gerekmez): satın alma sayfası, iyzico/PayTR dönüşleri, e-postalar,
 * "Lisanslarım" (e-posta bağlantısıyla giriş), dış satış bağlantısı ve yönetici ekranı (Siparişler, Paketler).
 *
 * wp-config.php sabitleri (yalnızca kullandığınız sağlayıcınınkiler):
 *   VERNUKO_IYZICO_API_KEY, VERNUKO_IYZICO_SECRET_KEY, VERNUKO_IYZICO_SANDBOX (true/false)
 *   VERNUKO_PAYTR_MERCHANT_ID, VERNUKO_PAYTR_MERCHANT_KEY, VERNUKO_PAYTR_MERCHANT_SALT, VERNUKO_PAYTR_TEST (true/false)
 *   VERNUKO_LICENSE_SALES_SECRET  (dış satış sistemi için, en az 32 karakter)
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

namespace Vernuko\License;

final class WpHttp implements Http
{
    public function post(string $url, array $headers, string|array $body): array
    {
        $r = wp_remote_post($url, ['headers' => $headers, 'body' => $body, 'timeout' => 30]);
        if (is_wp_error($r)) {
            return [0, ''];
        }
        return [(int) wp_remote_retrieve_response_code($r), (string) wp_remote_retrieve_body($r)];
    }
}

final class Checkout
{
    private const OPT_PLANS = 'vernuko_plans';
    private const OPT_SETTINGS = 'vernuko_sales_settings';
    private const COOKIE_ORDERS = 'vk_orders';
    private const COOKIE_SESSION = 'vk_session';

    public static function boot(): void
    {
        add_shortcode('vernuko_satin_al', [self::class, 'checkoutShortcode']);
        add_shortcode('vernuko_lisanslarim', [self::class, 'licensesShortcode']);
        add_action('template_redirect', [self::class, 'handlePost']);
        add_action('rest_api_init', [self::class, 'routes']);
        add_action('admin_menu', [self::class, 'adminMenu'], 20);
        add_action('admin_post_vernuko_sales', [self::class, 'adminAction']);
    }

    // ------------------------------------------------------------ configuration

    /** @return array<string,Plan> */
    public static function plans(): array
    {
        $out = [];
        foreach ((array) get_option(self::OPT_PLANS, []) as $id => $a) {
            if (is_array($a)) {
                $out[(string) $id] = Plan::fromArray((string) $id, $a);
            }
        }
        return $out;
    }

    public static function settings(): array
    {
        return wp_parse_args((array) get_option(self::OPT_SETTINGS, []), ['terms_url' => '', 'privacy_url' => '', 'licenses_url' => '', 'renew_url' => '']);
    }

    /** @return array<string,string> provider key => label, only the configured ones */
    public static function providers(): array
    {
        $p = [];
        if (self::iyzico() !== null) {
            $p['iyzico'] = 'Kredi/banka kartı (iyzico)';
        }
        if (self::paytr() !== null) {
            $p['paytr'] = 'Kredi/banka kartı (PayTR)';
        }
        return $p;
    }

    private static function iyzico(): ?Iyzico
    {
        if (!defined('VERNUKO_IYZICO_API_KEY') || !defined('VERNUKO_IYZICO_SECRET_KEY') || VERNUKO_IYZICO_API_KEY === '') {
            return null;
        }
        $base = defined('VERNUKO_IYZICO_BASE_URL') ? (string) VERNUKO_IYZICO_BASE_URL : (defined('VERNUKO_IYZICO_SANDBOX') && VERNUKO_IYZICO_SANDBOX ? Iyzico::SANDBOX : Iyzico::LIVE);
        return new Iyzico(new WpHttp(), (string) VERNUKO_IYZICO_API_KEY, (string) VERNUKO_IYZICO_SECRET_KEY, $base);
    }

    private static function paytr(): ?Paytr
    {
        if (!defined('VERNUKO_PAYTR_MERCHANT_ID') || !defined('VERNUKO_PAYTR_MERCHANT_KEY') || !defined('VERNUKO_PAYTR_MERCHANT_SALT') || VERNUKO_PAYTR_MERCHANT_ID === '') {
            return null;
        }
        return new Paytr(new WpHttp(), (string) VERNUKO_PAYTR_MERCHANT_ID, (string) VERNUKO_PAYTR_MERCHANT_KEY, (string) VERNUKO_PAYTR_MERCHANT_SALT, defined('VERNUKO_PAYTR_TEST') && VERNUKO_PAYTR_TEST,
            defined('VERNUKO_PAYTR_TOKEN_URL') ? (string) VERNUKO_PAYTR_TOKEN_URL : Paytr::TOKEN_URL); // override only for tests
    }

    private static function orders(): ?Orders
    {
        $issuer = Plugin::issuer();
        global $wpdb;
        return $issuer === null ? null : new Orders(new WpDb($wpdb), $issuer);
    }

    // ------------------------------------------------------------ checkout page

    /** [vernuko_satin_al plan="yillik"] */
    public static function checkoutShortcode($atts): string
    {
        $atts = shortcode_atts(['plan' => ''], (array) $atts);
        $plans = self::plans();
        $plan = $plans[$atts['plan']] ?? null;
        $orders = self::orders();
        if ($plan === null || $orders === null) {
            return '<p>' . esc_html($orders === null ? 'Satış henüz yapılandırılmadı.' : 'Paket bulunamadı: ' . $atts['plan']) . '</p>';
        }

        // Returning from the payment page
        $orderId = sanitize_text_field(wp_unslash($_GET['vernuko_order'] ?? ''));
        if ($orderId !== '') {
            return self::orderStatus($orders, $orderId);
        }

        $state = $GLOBALS['vernuko_checkout_state'] ?? null;
        if (is_array($state) && isset($state['paytr_token'])) {
            return '<script src="https://www.paytr.com/js/iframeResizer.min.js"></script><iframe src="https://www.paytr.com/odeme/guvenli/' . esc_attr($state['paytr_token']) . '" id="paytriframe" frameborder="0" scrolling="no" style="width:100%;min-height:600px"></iframe><script>iFrameResize({},"#paytriframe");</script>';
        }

        $providers = self::providers();
        if (!$providers) {
            return '<p>Ödeme yöntemi henüz yapılandırılmadı.</p>';
        }
        $s = self::settings();
        // Form fields carry a "vk_" prefix: names like "name" are WordPress query variables (they would open a 404).
        $v = static fn (string $k) => esc_attr(wp_unslash($_POST['vk_' . $k] ?? ($k === 'license_key' ? ($_GET['anahtar'] ?? '') : '')));
        $html = '<form method="post" class="vernuko-checkout">';
        if (is_array($state) && isset($state['error'])) {
            $html .= '<div class="vernuko-error" role="alert" style="color:#b00020;margin-bottom:1em">' . esc_html($state['error']) . '</div>';
        }
        $html .= '<h3>' . esc_html($plan->name) . ' — ' . esc_html($plan->priceText()) . '</h3>';
        $html .= '<p>' . esc_html($plan->edition) . ' · ' . $plan->devices . ' bilgisayar · ' . $plan->days . ' gün</p>';
        $html .= wp_nonce_field('vernuko_checkout_' . $plan->id, '_vknonce', true, false) . '<input type="hidden" name="vernuko_plan" value="' . esc_attr($plan->id) . '">';
        if ($plan->kind === 'renewal') {
            $html .= self::field('license_key', 'Yenilenecek lisans anahtarı', $v('license_key'), 'VRNK-XXXXX-XXXXX-XXXXX-XXXXX', true);
        }
        $html .= self::field('name', 'Ad soyad', $v('name'), '', true) . self::field('email', 'E-posta (anahtar buraya gönderilir)', $v('email'), '', true, 'email')
            . self::field('phone', 'Cep telefonu', $v('phone'), '0532 123 45 67', true, 'tel') . self::field('company', 'Firma unvanı (faturada görünür)', $v('company'))
            . self::field('tax_office', 'Vergi dairesi', $v('tax_office')) . self::field('tax_number', 'VKN / TCKN', $v('tax_number'))
            . self::field('address', 'Fatura adresi', $v('address'), '', true) . self::field('city', 'Şehir', $v('city'), '', true);
        $html .= '<p><strong>Ödeme yöntemi</strong><br>';
        $first = true;
        foreach ($providers as $key => $label) {
            $html .= '<label><input type="radio" name="vk_provider" value="' . esc_attr($key) . '"' . ($first ? ' checked' : '') . '> ' . esc_html($label) . '</label><br>';
            $first = false;
        }
        $html .= '</p>';
        $terms = $s['terms_url'] ? '<a href="' . esc_url($s['terms_url']) . '" target="_blank">Ön bilgilendirme formunu ve mesafeli satış sözleşmesini</a>' : 'Ön bilgilendirme formunu ve mesafeli satış sözleşmesini';
        $privacy = $s['privacy_url'] ? '<a href="' . esc_url($s['privacy_url']) . '" target="_blank">KVKK aydınlatma metnini</a>' : 'KVKK aydınlatma metnini';
        $html .= '<p><label><input type="checkbox" name="vk_terms" value="1" required> ' . $terms . ' okudum, kabul ediyorum.</label><br>';
        $html .= '<label><input type="checkbox" name="vk_privacy" value="1" required> ' . $privacy . ' okudum.</label></p>';
        $html .= '<p><button type="submit">Ödemeye geç</button></p></form>';
        return $html;
    }

    private static function field(string $name, string $label, string $value, string $placeholder = '', bool $required = false, string $type = 'text'): string
    {
        return '<p><label>' . esc_html($label) . ($required ? ' *' : '') . '<br><input type="' . esc_attr($type) . '" name="vk_' . esc_attr($name) . '" value="' . $value . '" placeholder="' . esc_attr($placeholder) . '"' . ($required ? ' required' : '') . ' style="width:100%"></label></p>';
    }

    /** Form POST (before output, so iyzico can be a redirect). */
    public static function handlePost(): void
    {
        if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && isset($_POST['vernuko_plan'])) {
            self::startPayment();
        }
        if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && isset($_POST['vernuko_link_email'])) {
            self::sendSignInLink();
        }
        if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && isset($_POST['vernuko_remove_device'])) {
            self::removeDevice();
        }
        if (isset($_GET['vk_giris'])) {
            self::signIn(sanitize_text_field(wp_unslash($_GET['vk_giris'])));
        }
    }

    private static function startPayment(): void
    {
        $planId = sanitize_key(wp_unslash($_POST['vernuko_plan']));
        $plan = self::plans()[$planId] ?? null;
        $orders = self::orders();
        $provider = sanitize_key(wp_unslash($_POST['vk_provider'] ?? ''));
        if ($plan === null || $orders === null || !wp_verify_nonce(sanitize_text_field(wp_unslash($_POST['_vknonce'] ?? '')), 'vernuko_checkout_' . $planId)) {
            $GLOBALS['vernuko_checkout_state'] = ['error' => 'Sayfanın süresi doldu; lütfen tekrar deneyin.'];
            return;
        }
        if (empty($_POST['vk_terms']) || empty($_POST['vk_privacy'])) {
            $GLOBALS['vernuko_checkout_state'] = ['error' => 'Devam etmek için sözleşmeyi ve aydınlatma metnini onaylayın.'];
            return;
        }
        if (!isset(self::providers()[$provider])) {
            $GLOBALS['vernuko_checkout_state'] = ['error' => 'Ödeme yöntemi seçin.'];
            return;
        }
        $form = [];
        foreach (['name', 'email', 'phone', 'company', 'tax_office', 'tax_number', 'address', 'city'] as $k) {
            $form[$k] = sanitize_text_field(wp_unslash($_POST['vk_' . $k] ?? ''));
        }
        $ip = (string) ($_SERVER['REMOTE_ADDR'] ?? '');
        try {
            $order = $orders->create($plan, $provider, $form, sanitize_text_field(wp_unslash($_POST['vk_license_key'] ?? '')) ?: null, $ip);
            $return = remove_query_arg(['vernuko_order', 'durum'], self::currentUrl());
            set_transient('vk_ret_' . $order['id'], $return, 3 * DAY_IN_SECONDS);
            self::rememberOrder($order['id']);
            if ($provider === 'iyzico') {
                $identity = strlen((string) $order['tax_number']) === 11 ? (string) $order['tax_number'] : '11111111111';
                $url = self::iyzico()->initialize($order, $plan, rest_url('vernuko/v1/pay/iyzico') . '?order=' . rawurlencode($order['id']), $identity);
                wp_redirect($url);
                exit;
            }
            $back = add_query_arg('vernuko_order', $order['id'], $return);
            $GLOBALS['vernuko_checkout_state'] = ['paytr_token' => self::paytr()->iframeToken($order, $plan, add_query_arg('durum', 'ok', $back), add_query_arg('durum', 'hata', $back))];
        } catch (Error $e) {
            $GLOBALS['vernuko_checkout_state'] = ['error' => $e->getMessage()];
        }
    }

    private static function orderStatus(Orders $orders, string $orderId): string
    {
        try {
            $order = $orders->find($orderId);
        } catch (Error) {
            return '<p>Sipariş bulunamadı.</p>';
        }
        $issuer = Plugin::issuer();
        if ($order['status'] === 'paid') {
            $html = '<div class="vernuko-paid"><h3>Ödemeniz alındı, teşekkürler.</h3>';
            // The key is shown only in the browser that placed the order; it is always e-mailed too.
            if (self::isMyOrder($orderId)) {
                foreach ($issuer->orderLicenses("own:$orderId") as $l) {
                    $html .= '<p>Lisans anahtarınız: <code style="font-size:1.2em">' . esc_html($issuer->keyFor($l['id'])) . '</code></p>';
                }
                foreach ($issuer->orderRenewals("own:$orderId") as $l) {
                    $html .= '<p>Lisansınız ' . esc_html(wp_date('d.m.Y', (int) $l['paid_until'])) . ' tarihine kadar uzatıldı. Program bir sonraki çevrim içi kontrolde (veya Ayarlar › Lisans › Şimdi doğrula) yeni tarihi alır.</p>';
                }
            }
            return $html . '<p>Anahtar ve bilgiler <strong>' . esc_html($order['email']) . '</strong> adresine e-postayla gönderildi. Vernuko\'da Ayarlar › Lisans › "Lisans anahtarı" alanına girip Etkinleştir\'e basın.</p></div>';
        }
        if (in_array($order['status'], ['failed', 'mismatch'], true) || ($_GET['durum'] ?? '') === 'hata') {
            return '<p>Ödeme tamamlanamadı. Kartınızdan para çekildiyse bizimle iletişime geçin (sipariş no: ' . esc_html($orderId) . ').</p>';
        }
        if ($order['status'] === 'refunded') {
            return '<p>Bu sipariş iade edildi.</p>';
        }
        // PayTR confirms server-to-server; the page waits a moment for that notification.
        return '<meta http-equiv="refresh" content="4"><p>Ödemeniz doğrulanıyor… Bu sayfa kendiliğinden yenilenir.</p>';
    }

    // ------------------------------------------------------------ provider returns (REST)

    public static function routes(): void
    {
        register_rest_route(Plugin::REST_NAMESPACE, '/pay/iyzico', ['methods' => 'POST', 'permission_callback' => '__return_true', 'callback' => [self::class, 'iyzicoReturn']]);
        register_rest_route(Plugin::REST_NAMESPACE, '/pay/paytr', ['methods' => 'POST', 'permission_callback' => '__return_true', 'callback' => [self::class, 'paytrNotify']]);
        foreach (['sales', 'sales/revoke'] as $route) {
            register_rest_route(Plugin::REST_NAMESPACE, '/' . $route, ['methods' => 'POST', 'permission_callback' => '__return_true',
                'callback' => static fn (\WP_REST_Request $r) => self::externalSale('v1/' . $route, $r)]);
        }
    }

    /** The customer's browser comes back with the token; the result is asked from iyzico and its signature checked. */
    public static function iyzicoReturn(\WP_REST_Request $request)
    {
        $orderId = sanitize_text_field((string) $request->get_param('order'));
        $token = sanitize_text_field((string) $request->get_param('token'));
        $orders = self::orders();
        $iyzico = self::iyzico();
        $back = (string) get_transient('vk_ret_' . $orderId) ?: home_url('/');
        if ($orders !== null && $iyzico !== null && $token !== '') {
            try {
                $r = $iyzico->retrieve($token);
                if ($r['orderId'] !== $orderId) {
                    throw new Error(400, 'pay.order', 'Ödeme başka bir siparişe ait.');
                }
                if ($r['paid']) {
                    self::onPaid($orders->paid($orderId, 'iyzico', $r['paymentId'], $r['priceKurus'], $r['currency'], self::planOf($orders->find($orderId))));
                } else {
                    $orders->failed($orderId, $r['reason']);
                }
            } catch (Error $e) {
                Plugin::issuer()?->audit('order.iyzico_error', null, "$orderId: " . $e->getMessage());
            }
        }
        wp_redirect(add_query_arg('vernuko_order', $orderId, $back));
        exit;
    }

    /** PayTR's server notification ("Bildirim URL"): answer exactly "OK" once handled (also for repeats). */
    public static function paytrNotify(\WP_REST_Request $request)
    {
        $orders = self::orders();
        $paytr = self::paytr();
        $post = array_map(static fn ($v) => is_string($v) ? wp_unslash($v) : '', $request->get_body_params());
        header('Content-Type: text/plain; charset=utf-8');
        if ($orders === null || $paytr === null) {
            echo 'PAYTR notification failed: not configured';
            exit;
        }
        try {
            $n = $paytr->notification($post);
        } catch (Error $e) {
            echo $e->getMessage();
            exit;
        }
        try {
            if ($n['paid']) {
                self::onPaid($orders->paid($n['orderId'], 'paytr', 'paytr:' . $n['orderId'], $n['totalKurus'], $n['currency'], self::planOf($orders->find($n['orderId']))));
            } else {
                $orders->failed($n['orderId'], $n['reason']);
            }
        } catch (Error $e) {
            // Unknown order or amount mismatch: recorded; acknowledge so PayTR stops repeating.
            Plugin::issuer()?->audit('order.paytr_error', null, $n['orderId'] . ': ' . $e->getMessage());
        }
        echo 'OK';
        exit;
    }

    private static function planOf(array $order): Plan
    {
        $plan = self::plans()[$order['plan_id']] ?? null;
        if ($plan === null) {
            throw new Error(400, 'order.plan', 'Siparişin paketi artık tanımlı değil.');
        }
        return $plan;
    }

    /** First confirmation only: e-mail the key (or the new end date) to the customer. */
    private static function onPaid(array $result): void
    {
        if (!$result['granted']) {
            return;
        }
        $issuer = Plugin::issuer();
        $order = $result['order'];
        $lines = ["Merhaba {$order['customer_name']},", '', 'Vernuko siparişiniz için teşekkürler (sipariş no: ' . $order['id'] . ').', ''];
        foreach ($result['licenses'] as $l) {
            $lines[] = 'Lisans anahtarınız: ' . $issuer->keyFor($l['id']);
            $lines[] = 'Geçerlilik: ' . wp_date('d.m.Y', (int) $l['paid_until']) . ' · ' . (int) $l['max_devices'] . ' bilgisayar';
        }
        foreach ($result['renewed'] as $l) {
            $lines[] = 'Lisansınız ' . wp_date('d.m.Y', (int) $l['paid_until']) . ' tarihine kadar uzatıldı.';
        }
        $lines[] = '';
        $lines[] = "Etkinleştirme: Vernuko'da Ayarlar › Lisans › \"Lisans anahtarı\" alanına girip Etkinleştir'e basın.";
        $s = self::settings();
        if ($s['licenses_url']) {
            $lines[] = 'Lisanslarınızı görmek ve bilgisayar değiştirmek için: ' . $s['licenses_url'];
        }
        wp_mail($order['email'], 'Vernuko lisansınız', implode("\n", $lines));
    }

    private static function externalSale(string $route, \WP_REST_Request $request): \WP_REST_Response
    {
        $issuer = Plugin::issuer();
        if ($issuer === null || !defined('VERNUKO_LICENSE_SALES_SECRET') || strlen((string) VERNUKO_LICENSE_SALES_SECRET) < 32) {
            return new \WP_REST_Response(['code' => 'sales.not_configured', 'message' => 'Dış satış bağlantısı yapılandırılmadı.'], 503);
        }
        [$status, $body] = (new ExternalSales($issuer, (string) VERNUKO_LICENSE_SALES_SECRET))->handle($route, $request->get_header('x_vernuko_signature'), $request->get_body());
        return new \WP_REST_Response($body, $status);
    }

    // ------------------------------------------------------------ "Lisanslarım" without an account

    /** [vernuko_lisanslarim] */
    public static function licensesShortcode(): string
    {
        $issuer = Plugin::issuer();
        if ($issuer === null) {
            return '<p>Lisanslar şu anda gösterilemiyor.</p>';
        }
        $email = self::sessionEmail();
        if ($email === null) {
            $sent = !empty($GLOBALS['vernuko_link_sent']);
            return ($sent ? '<p>Bu e-posta adresine kayıtlı lisans varsa giriş bağlantısı gönderildi (30 dakika geçerli). Gelen kutunuzu ve istenmeyen klasörünü kontrol edin.</p>' : '')
                . '<form method="post">' . wp_nonce_field('vernuko_link', '_vknonce', true, false)
                . '<p><label>Lisansı satın aldığınız e-posta adresi<br><input type="email" name="vernuko_link_email" required style="width:100%"></label></p><p><button type="submit">Giriş bağlantısı gönder</button></p></form>';
        }
        $s = self::settings();
        $html = '<p>' . esc_html($email) . ' adresine ait lisanslar.</p>';
        if (!empty($_GET['vk_kaldirildi'])) {
            $html .= '<p><strong>Bilgisayar kaldırıldı; lisansı yeni bilgisayarda etkinleştirebilirsiniz.</strong></p>';
        }
        $licenses = $issuer->licenses($email);
        $licenses = array_values(array_filter($licenses, static fn ($l) => strtolower((string) $l['customer_email']) === $email));
        if (!$licenses) {
            return $html . '<p>Bu adrese ait lisans yok.</p>';
        }
        foreach ($licenses as $l) {
            $key = $issuer->keyFor($l['id']);
            $active = $issuer->effectiveEnd($l) > time();
            $html .= '<div style="border:1px solid #ddd;border-radius:6px;padding:14px;margin:0 0 16px"><p><code style="font-size:1.15em">' . esc_html($key) . '</code></p>';
            $html .= '<p>' . esc_html($l['edition']) . ' · ' . (int) $l['max_devices'] . ' bilgisayar · ' . ($active ? 'Geçerlilik: ' : '<strong>Süresi doldu:</strong> ') . esc_html(wp_date('d.m.Y', (int) $l['paid_until'])) . ($l['status'] === 'cancelled' ? ' · İptal' : '') . '</p>';
            foreach ($issuer->activations($l['id']) as $a) {
                if ($a['deactivated_at'] !== null) {
                    continue;
                }
                $html .= '<form method="post" style="margin:4px 0">' . wp_nonce_field('vernuko_device', '_vknonce', true, false) . '<input type="hidden" name="vernuko_remove_device" value="' . esc_attr($a['id']) . '">'
                    . esc_html($a['machine_label'] ?: 'Bilgisayar') . ' — son görülme ' . esc_html(wp_date('d.m.Y', (int) $a['last_seen'])) . ' <button type="submit" onclick="return confirm(\'Bu bilgisayardaki lisans kaldırılsın mı? Oradaki veriler silinmez; program salt-okunur olur.\')">Kaldır</button></form>';
            }
            if ($s['renew_url'] && $l['status'] !== 'cancelled') {
                $html .= '<p><a class="button" href="' . esc_url(add_query_arg('anahtar', $key, $s['renew_url'])) . '">Yenile</a></p>';
            }
            $html .= '</div>';
        }
        return $html;
    }

    private static function sendSignInLink(): void
    {
        if (!wp_verify_nonce(sanitize_text_field(wp_unslash($_POST['_vknonce'] ?? '')), 'vernuko_link')) {
            return;
        }
        $email = strtolower(sanitize_email(wp_unslash($_POST['vernuko_link_email'] ?? '')));
        $issuer = Plugin::issuer();
        $GLOBALS['vernuko_link_sent'] = true; // same answer whether or not the address has licenses
        $ipKey = 'vk_link_' . md5(($_SERVER['REMOTE_ADDR'] ?? '-') . '|' . gmdate('YmdH'));
        if ($issuer === null || !Validate::email($email) || (int) get_transient($ipKey) >= 5) {
            return;
        }
        set_transient($ipKey, (int) get_transient($ipKey) + 1, HOUR_IN_SECONDS);
        $owned = array_filter($issuer->licenses($email), static fn ($l) => strtolower((string) $l['customer_email']) === $email);
        if (!$owned) {
            return;
        }
        $url = add_query_arg('vk_giris', MagicLink::create($issuer->keySecret(), $email, time()), remove_query_arg(['vk_giris', 'vk_kaldirildi'], self::currentUrl()));
        wp_mail($email, 'Vernuko lisanslarınız — giriş bağlantısı', "Lisanslarınızı görmek için 30 dakika içinde bu bağlantıyı açın:\n\n$url\n\nBu isteği siz yapmadıysanız e-postayı yok sayabilirsiniz.");
    }

    private static function signIn(string $token): void
    {
        $issuer = Plugin::issuer();
        if ($issuer === null || MagicLink::verify($issuer->keySecret(), $token, time()) === null) {
            return;
        }
        setcookie(self::COOKIE_SESSION, $token, ['expires' => time() + 1800, 'path' => COOKIEPATH ?: '/', 'secure' => is_ssl(), 'httponly' => true, 'samesite' => 'Lax']);
        wp_safe_redirect(remove_query_arg('vk_giris', self::currentUrl()));
        exit;
    }

    private static function sessionEmail(): ?string
    {
        $issuer = Plugin::issuer();
        $token = isset($_COOKIE[self::COOKIE_SESSION]) ? sanitize_text_field(wp_unslash($_COOKIE[self::COOKIE_SESSION])) : '';
        return $issuer === null || $token === '' ? null : MagicLink::verify($issuer->keySecret(), $token, time());
    }

    private static function removeDevice(): void
    {
        $email = self::sessionEmail();
        $issuer = Plugin::issuer();
        if ($email === null || $issuer === null || !wp_verify_nonce(sanitize_text_field(wp_unslash($_POST['_vknonce'] ?? '')), 'vernuko_device')) {
            return;
        }
        $activationId = sanitize_text_field(wp_unslash($_POST['vernuko_remove_device']));
        foreach ($issuer->licenses($email) as $l) {
            if (strtolower((string) $l['customer_email']) !== $email) {
                continue;
            }
            foreach ($issuer->activations($l['id']) as $a) {
                if ($a['id'] === $activationId) {
                    $issuer->removeActivation($activationId, 'müşteri, Lisanslarım');
                    wp_safe_redirect(add_query_arg('vk_kaldirildi', '1', self::currentUrl()));
                    exit;
                }
            }
        }
    }

    // ------------------------------------------------------------ helpers

    private static function currentUrl(): string
    {
        return home_url(add_query_arg([], wp_unslash($_SERVER['REQUEST_URI'] ?? '/')));
    }

    private static function rememberOrder(string $orderId): void
    {
        $ids = self::myOrders();
        $ids[] = $orderId;
        $value = implode(',', array_slice(array_unique($ids), -10));
        $sig = hash_hmac('sha256', $value, wp_salt('auth'));
        setcookie(self::COOKIE_ORDERS, $value . '|' . $sig, ['expires' => time() + 7 * DAY_IN_SECONDS, 'path' => COOKIEPATH ?: '/', 'secure' => is_ssl(), 'httponly' => true, 'samesite' => 'Lax']);
        $_COOKIE[self::COOKIE_ORDERS] = $value . '|' . $sig;
    }

    /** @return string[] */
    private static function myOrders(): array
    {
        $raw = isset($_COOKIE[self::COOKIE_ORDERS]) ? sanitize_text_field(wp_unslash($_COOKIE[self::COOKIE_ORDERS])) : '';
        [$value, $sig] = array_pad(explode('|', $raw, 2), 2, '');
        return $value !== '' && hash_equals(hash_hmac('sha256', $value, wp_salt('auth')), $sig) ? explode(',', $value) : [];
    }

    private static function isMyOrder(string $orderId): bool
    {
        return in_array($orderId, self::myOrders(), true);
    }

    // ------------------------------------------------------------ admin: Siparişler, Paketler

    public static function adminMenu(): void
    {
        $cap = current_user_can('manage_woocommerce') ? 'manage_woocommerce' : 'manage_options';
        add_submenu_page('vernuko-license', 'Siparişler', 'Siparişler', $cap, 'vernuko-orders', [self::class, 'ordersPage']);
        add_submenu_page('vernuko-license', 'Paketler ve satış', 'Paketler ve satış', $cap, 'vernuko-plans', [self::class, 'plansPage']);
    }

    private static function can(): bool
    {
        return current_user_can('manage_woocommerce') || current_user_can('manage_options');
    }

    public static function ordersPage(): void
    {
        if (!self::can()) {
            wp_die('Yetkiniz yok.', 403);
        }
        $orders = self::orders();
        echo '<div class="wrap"><h1>Siparişler</h1>';
        self::notice();
        if ($orders === null) {
            echo '<p>Önce Vernuko Lisans sayfasında kurulumu tamamlayın.</p></div>';
            return;
        }
        $month = gmdate('Y-m');
        echo '<form method="post" action="' . esc_url(admin_url('admin-post.php')) . '">' . wp_nonce_field('vernuko_sales', '_wpnonce', true, false)
            . '<input type="hidden" name="action" value="vernuko_sales"><input type="hidden" name="op" value="csv"><input type="month" name="month" value="' . esc_attr($month) . '"> <button class="button">Muhasebe için CSV indir</button></form>';
        echo '<p>Faturalar bu eklentide kesilmez; CSV\'yi mali müşavirinize veya e-Arşiv programınıza verin.</p>';
        echo '<table class="widefat striped"><thead><tr><th>Sipariş</th><th>Tarih</th><th>Müşteri</th><th>Paket</th><th>Tutar</th><th>Ödeme</th><th>Durum</th><th></th></tr></thead><tbody>';
        $labels = ['pending' => 'Ödeme bekleniyor', 'paid' => 'Ödendi', 'failed' => 'Başarısız', 'mismatch' => 'Tutar uyuşmadı', 'refunded' => 'İade'];
        foreach ($orders->list(sanitize_text_field(wp_unslash($_GET['s'] ?? '')) ?: null) as $o) {
            echo '<tr><td><code>' . esc_html($o['id']) . '</code></td><td>' . esc_html(wp_date('d.m.Y H:i', (int) $o['created_at'])) . '</td><td>' . esc_html($o['customer_name']) . '<br><small>' . esc_html($o['email'] . ($o['company'] ? ' · ' . $o['company'] : '')) . '</small></td>';
            echo '<td>' . esc_html($o['plan_id']) . '</td><td>' . esc_html(number_format($o['amount'] / 100, 2, ',', '.')) . ' TL</td><td>' . esc_html($o['provider']) . '</td><td>' . esc_html($labels[$o['status']] ?? $o['status']) . '</td><td>';
            if ($o['status'] === 'paid') {
                echo '<form method="post" action="' . esc_url(admin_url('admin-post.php')) . '">' . wp_nonce_field('vernuko_sales', '_wpnonce', true, false) . '<input type="hidden" name="action" value="vernuko_sales"><input type="hidden" name="op" value="refund"><input type="hidden" name="order" value="' . esc_attr($o['id']) . '"><button class="button button-small" onclick="return confirm(\'Parayı ödeme sağlayıcısının panelinden iade ettiniz mi? Lisans durdurulacak.\')">İade edildi</button></form>';
            }
            echo '</td></tr>';
        }
        echo '</tbody></table></div>';
    }

    public static function plansPage(): void
    {
        if (!self::can()) {
            wp_die('Yetkiniz yok.', 403);
        }
        $s = self::settings();
        echo '<div class="wrap"><h1>Paketler ve satış</h1>';
        self::notice();
        $providers = self::providers();
        echo '<p><strong>Ödeme yöntemleri:</strong> ' . ($providers ? esc_html(implode(', ', $providers)) : 'yapılandırılmadı (wp-config.php sabitleri, README\'ye bakın)') . '</p>';
        echo '<p>PayTR mağaza panelinde Bildirim URL: <code>' . esc_html(rest_url('vernuko/v1/pay/paytr')) . '</code></p>';
        echo '<p>Dış satış sistemi: <code>' . esc_html(rest_url('vernuko/v1/sales')) . '</code> ' . (defined('VERNUKO_LICENSE_SALES_SECRET') ? '(açık)' : '(kapalı — VERNUKO_LICENSE_SALES_SECRET tanımlı değil)') . '</p>';
        $form = '<form method="post" action="' . esc_url(admin_url('admin-post.php')) . '">' . wp_nonce_field('vernuko_sales', '_wpnonce', true, false) . '<input type="hidden" name="action" value="vernuko_sales"><input type="hidden" name="op" value="save">';
        $form .= '<table class="widefat striped"><thead><tr><th>Kısa ad (kod)</th><th>Ad</th><th>Fiyat (TL, KDV dahil)</th><th>Tür</th><th>Paket</th><th>Bilgisayar</th><th>Gün</th></tr></thead><tbody>';
        $rows = self::plans() + ['' => null];
        $i = 0;
        foreach ($rows as $id => $p) {
            $form .= '<tr><td><input name="plans[' . $i . '][id]" value="' . esc_attr((string) $id) . '" placeholder="yillik"></td><td><input name="plans[' . $i . '][name]" value="' . esc_attr($p?->name ?? '') . '"></td>'
                . '<td><input name="plans[' . $i . '][price]" value="' . esc_attr($p ? number_format($p->priceKurus / 100, 2, ',', '') : '') . '" style="width:100px"></td>'
                . '<td><select name="plans[' . $i . '][kind]"><option value="new">Yeni lisans</option><option value="renewal"' . ($p?->kind === 'renewal' ? ' selected' : '') . '>Yenileme</option></select></td>'
                . '<td><input name="plans[' . $i . '][edition]" value="' . esc_attr($p?->edition ?? 'Standart') . '" style="width:100px"></td><td><input type="number" min="1" max="50" name="plans[' . $i . '][devices]" value="' . esc_attr((string) ($p?->devices ?? 1)) . '" style="width:60px"></td>'
                . '<td><input type="number" min="1" name="plans[' . $i . '][days]" value="' . esc_attr((string) ($p?->days ?? 365)) . '" style="width:70px"></td></tr>';
            $i++;
        }
        $form .= '</tbody></table><p>Kısa adı boşaltılan satır silinir. Sayfaya eklemek için: <code>[vernuko_satin_al plan="yillik"]</code> · Lisanslarım sayfası: <code>[vernuko_lisanslarim]</code></p>';
        $form .= '<h2>Bağlantılar</h2><table class="form-table">';
        foreach (['terms_url' => 'Mesafeli satış sözleşmesi + ön bilgilendirme sayfası', 'privacy_url' => 'KVKK aydınlatma metni sayfası', 'licenses_url' => '[vernuko_lisanslarim] sayfası', 'renew_url' => 'Yenileme paketinin [vernuko_satin_al] sayfası'] as $k => $label) {
            $form .= '<tr><th>' . esc_html($label) . '</th><td><input type="url" name="settings[' . $k . ']" value="' . esc_attr($s[$k]) . '" style="width:420px"></td></tr>';
        }
        echo $form . '</table><p><button class="button button-primary">Kaydet</button></p></form></div>';
    }

    public static function adminAction(): void
    {
        check_admin_referer('vernuko_sales');
        if (!self::can()) {
            wp_die('Yetkiniz yok.', 403);
        }
        $op = sanitize_key($_POST['op'] ?? '');
        $back = admin_url('admin.php?page=vernuko-orders');
        $notice = '';
        try {
            if ($op === 'save') {
                $plans = [];
                foreach ((array) ($_POST['plans'] ?? []) as $row) {
                    $id = sanitize_key(wp_unslash($row['id'] ?? ''));
                    if ($id === '') {
                        continue;
                    }
                    $price = (int) round((float) str_replace(['.', ','], ['', '.'], sanitize_text_field(wp_unslash($row['price'] ?? '0'))) * 100);
                    $plans[$id] = Plan::fromArray($id, ['name' => sanitize_text_field(wp_unslash($row['name'] ?? $id)), 'price_kurus' => $price, 'kind' => sanitize_key($row['kind'] ?? 'new'),
                        'edition' => sanitize_text_field(wp_unslash($row['edition'] ?? 'Standart')), 'devices' => absint($row['devices'] ?? 1), 'days' => absint($row['days'] ?? 365)])->toArray();
                }
                update_option(self::OPT_PLANS, $plans, false);
                $settings = [];
                foreach (['terms_url', 'privacy_url', 'licenses_url', 'renew_url'] as $k) {
                    $settings[$k] = esc_url_raw(wp_unslash($_POST['settings'][$k] ?? ''));
                }
                update_option(self::OPT_SETTINGS, $settings, false);
                $notice = 'Kaydedildi.';
                $back = admin_url('admin.php?page=vernuko-plans');
            } elseif ($op === 'refund' && ($orders = self::orders()) !== null) {
                $orders->refunded(sanitize_text_field(wp_unslash($_POST['order'] ?? '')), wp_get_current_user()->user_login);
                $notice = 'Sipariş iade edildi; lisans durduruldu.';
            } elseif ($op === 'csv' && ($orders = self::orders()) !== null) {
                $month = preg_match('/^\d{4}-\d{2}$/', (string) ($_POST['month'] ?? '')) ? (string) $_POST['month'] : gmdate('Y-m');
                $from = strtotime($month . '-01 00:00:00 UTC');
                $to = strtotime('+1 month', $from);
                nocache_headers();
                header('Content-Type: text/csv; charset=utf-8');
                header('Content-Disposition: attachment; filename="vernuko-satislar-' . $month . '.csv"');
                echo $orders->csv($from, $to);
                exit;
            }
        } catch (Error $e) {
            $notice = $e->getMessage();
        }
        set_transient('vernuko_sales_notice_' . get_current_user_id(), $notice, 300);
        wp_safe_redirect($back);
        exit;
    }

    private static function notice(): void
    {
        if ($n = get_transient('vernuko_sales_notice_' . get_current_user_id())) {
            delete_transient('vernuko_sales_notice_' . get_current_user_id());
            echo '<div class="notice notice-info"><p>' . esc_html($n) . '</p></div>';
        }
    }
}
