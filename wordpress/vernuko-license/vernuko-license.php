<?php
/**
 * Plugin Name:       Vernuko Lisans
 * Description:       Vernuko masaüstü uygulamasının lisans sunucusu: kendi satış sayfası (iyzico/PayTR) veya WooCommerce ile satış, dış satış sistemleri için bağlantı, lisans anahtarı, etkinleştirme, yenileme, bilgisayar taşıma ve güncelleme izni. Müşterilerin randevu/müşteri verisi bu siteye gelmez.
 * Version:           1.2.0
 * Requires at least: 6.4
 * Requires PHP:      8.1
 * Author:            Vernuko
 * License:           AGPL-3.0-only
 * License URI:       https://www.gnu.org/licenses/agpl-3.0.html
 * Text Domain:       vernuko-license
 * WC requires at least: 8.0
 */

if (!defined('ABSPATH')) {
    exit;
}

require_once __DIR__ . '/src/Core.php';
require_once __DIR__ . '/src/WpDb.php';
require_once __DIR__ . '/src/Sales.php';
require_once __DIR__ . '/src/Payments.php';
require_once __DIR__ . '/src/Plugin.php';
require_once __DIR__ . '/src/Checkout.php';

register_activation_hook(__FILE__, [Vernuko\License\Plugin::class, 'activate']);
Vernuko\License\Plugin::boot(__FILE__);
Vernuko\License\Checkout::boot();
