<?php
/**
 * Test/geliştirme sunucusu: eklentinin uç noktalarını WordPress olmadan sunar (php -S ... dev-server.php).
 * Adres yapısı WordPress'tekiyle aynı: /wp-json/vernuko/v1/... Yalnızca testler içindir; sitede kullanılmaz.
 *
 * Ortam değişkenleri: VK_DB (SQLite dosyası), VK_KEY (özel anahtar PEM dosyası), VK_SECRET (anahtar sırrı),
 * VK_CLOCK (saat dosyası: içinde unix zamanı; yoksa gerçek saat), VK_RELEASES (güncelleme dosyaları klasörü).
 */

require __DIR__ . '/../src/Core.php';

use Vernuko\License\Api;
use Vernuko\License\Issuer;
use Vernuko\License\PdoDb;
use Vernuko\License\Signer;

$db = new PdoDb(new PDO('sqlite:' . getenv('VK_DB')));
$db->createSchema();
$clockFile = getenv('VK_CLOCK') ?: '';
$clock = static fn (): int => $clockFile !== '' && is_file($clockFile) ? (int) trim(file_get_contents($clockFile)) : time();
$issuer = new Issuer($db, Signer::fromPem(file_get_contents(getenv('VK_KEY')), 'test-php'), getenv('VK_SECRET'), clock: $clock);

$path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
$prefix = '/wp-json/vernuko/';
header('Content-Type: application/json; charset=utf-8');
if (!str_starts_with($path, $prefix)) {
    http_response_code(404);
    echo json_encode(['code' => 'rest_no_route', 'message' => 'Yok']);
    return;
}

$route = substr($path, strlen($prefix));
$body = json_decode(file_get_contents('php://input') ?: '[]', true) ?: [];

// Yalnızca testler: lisans oluşturma (sitede bu, WooCommerce siparişi veya yönetici ekranıdır).
if ($route === 'test/create') {
    echo json_encode($issuer->createLicense($body['name'] ?? 'Test', $body['email'] ?? null, null, 'Standart', (int) ($body['devices'] ?? 1), (int) ($body['days'] ?? 365), 'test'));
    return;
}

if (str_starts_with($route, 'v1/updates/feed/')) {
    $permission = $issuer->updatePermission($_SERVER['HTTP_X_VERNUKO_ACTIVATION'] ?? null, $_SERVER['HTTP_X_VERNUKO_DEVICE'] ?? null);
    $file = Api::safeReleaseFile(getenv('VK_RELEASES') ?: '', substr($route, strlen('v1/updates/feed/')));
    if (!$permission['allowed'] || $file === null) {
        http_response_code($permission['allowed'] ? 404 : 403);
        echo json_encode(['code' => 'updates.not_allowed', 'message' => $permission['message']]);
        return;
    }
    header('Content-Type: application/octet-stream');
    readfile($file);
    return;
}

[$status, $response] = (new Api($issuer))->handle($route, $body);
http_response_code($status);
echo json_encode($response, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
