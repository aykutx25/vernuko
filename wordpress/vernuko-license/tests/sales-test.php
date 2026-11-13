<?php
/** Satış, ödeme sağlayıcıları ve dış satış bağlantısı testleri: php tests/sales-test.php */

require __DIR__ . '/../src/Core.php';
require __DIR__ . '/../src/Sales.php';
require __DIR__ . '/../src/Payments.php';

use Vernuko\License\{Error, ExternalSales, Http, Iyzico, Issuer, Keys, MagicLink, Orders, Paytr, PdoDb, Plan, Signer, Validate};

$failures = 0;
$count = 0;
function check(bool $ok, string $what): void
{
    global $failures, $count;
    $count++;
    if (!$ok) {
        $failures++;
        fwrite(STDERR, "FAIL: $what\n");
    }
}

final class FakeHttp implements Http
{
    public array $calls = [];
    /** @var callable */
    public $respond;

    public function post(string $url, array $headers, string|array $body): array
    {
        $this->calls[] = [$url, $headers, $body];
        return ($this->respond)($url, $headers, $body);
    }
}

[$pem] = Signer::generate();
$now = 1794466800; // 2026-11-13T07:00:00Z
$clock = static function () use (&$now): int {
    return $now;
};
$db = new PdoDb(new PDO('sqlite::memory:'));
$db->createSchema();
$issuer = new Issuer($db, Signer::fromPem($pem, 'k1'), random_bytes(32), clock: $clock);
$orders = new Orders($db, $issuer);
$plan = new Plan('yillik', 'Vernuko Yıllık', 499000, 'new', 'Standart', 2, 365);
$renewal = new Plan('yenileme', 'Vernuko Yenileme', 399000, 'renewal', 'Standart', 2, 365);
$form = ['name' => 'Işıl Güzel', 'email' => 'isil@example.com', 'phone' => '0532 123 45 67', 'company' => 'Işıl Güzellik Ltd.', 'tax_office' => 'Kadıköy', 'tax_number' => '', 'address' => 'Moda Cd. 1', 'city' => 'İstanbul'];

// Doğrulamalar
check(Validate::tckn('10000000146') && !Validate::tckn('10000000147'), 'TCKN');
check(Validate::vkn('0000000001') && !Validate::vkn('0000000002'), 'VKN');
check(Validate::phone('0532 123 45 67') === '+905321234567' && Validate::phone('123') === null, 'telefon');
try {
    $orders->create($plan, 'paytr', ['name' => 'X'] + $form, null, '1.2.3.4');
    check(false, 'eksik ad reddedilmeli');
} catch (Error $e) {
    check($e->errorCode === 'order.invalid' && str_contains($e->getMessage(), 'Ad ve soyad'), 'eksik ad mesajı');
}

// ---------------- PayTR
$http = new FakeHttp();
$paytr = new Paytr($http, '123456', 'KEY123', 'SALT456', testMode: true);
$http->respond = fn () => [200, '{"status":"success","token":"tok-paytr"}'];
$order = $orders->create($plan, 'paytr', $form, null, '81.2.3.4');
check(preg_match('/^VK[0-9A-F]{20}$/', $order['id']) === 1, 'sipariş no harf+rakam (PayTR kuralı)');
check($paytr->iframeToken($order, $plan, 'https://site/ok', 'https://site/fail') === 'tok-paytr', 'PayTR token');
$sent = $http->calls[0][2];
$hashStr = '123456' . '81.2.3.4' . $order['id'] . 'isil@example.com' . '499000' . $sent['user_basket'] . '0' . '0' . 'TL' . '1';
check($sent['paytr_token'] === base64_encode(hash_hmac('sha256', $hashStr . 'SALT456', 'KEY123', true)), 'paytr_token belgedeki formülle');
check(json_decode(base64_decode($sent['user_basket']), true) === [['Vernuko Yıllık', '4990.00', 1]], 'sepet biçimi');
check(!isset($sent['merchant_key']) && !isset($sent['merchant_salt']), 'sırlar istekte gönderilmez');

$notify = static function (string $oid, string $status, string $total) {
    return ['merchant_oid' => $oid, 'status' => $status, 'total_amount' => $total, 'payment_amount' => '499000', 'currency' => 'TL',
        'hash' => base64_encode(hash_hmac('sha256', $oid . 'SALT456' . $status . $total, 'KEY123', true))];
};
try {
    $paytr->notification(['hash' => 'x'] + $notify($order['id'], 'success', '499000'));
    check(false, 'sahte hash reddedilmeli');
} catch (Error $e) {
    check($e->errorCode === 'pay.paytr_hash', 'sahte bildirim reddedildi');
}
$n = $paytr->notification($notify($order['id'], 'success', '499000'));
$r = $orders->paid($n['orderId'], 'paytr', 'paytr', $n['totalKurus'], $n['currency'], $plan);
check($r['order']['status'] === 'paid' && count($r['licenses']) === 1 && (int) $r['licenses'][0]['max_devices'] === 2, 'PayTR ödemesi → 1 lisans');
$r2 = $orders->paid($n['orderId'], 'paytr', 'paytr', $n['totalKurus'], $n['currency'], $plan);
check(count($r2['licenses']) === 1 && count($issuer->licenses('isil@example.com')) === 1, 'tekrar gelen bildirim ikinci lisans üretmez');
check($r['granted'] === true && $r2['granted'] === false, 'e-posta yalnızca ilk onayda');

// Eksik ödeme lisans üretmez
$cheap = $orders->create($plan, 'paytr', $form, null, '81.2.3.4');
try {
    $orders->paid($cheap['id'], 'paytr', 'paytr', 100, 'TRY', $plan);
    check(false, 'eksik tutar reddedilmeli');
} catch (Error $e) {
    check($e->errorCode === 'order.amount' && $orders->find($cheap['id'])['status'] === 'mismatch' && $issuer->orderLicenses('own:' . $cheap['id']) === [], 'eksik tutar → lisans yok');
}
// Taksit faizi (fazla tutar) kabul
$inst = $orders->create($plan, 'paytr', $form, null, '81.2.3.4');
check(count($orders->paid($inst['id'], 'paytr', 'paytr', 520000, 'TRY', $plan)['licenses']) === 1, 'taksit farkıyla fazla ödeme kabul');

// ---------------- iyzico
$http = new FakeHttp();
$iyz = new Iyzico($http, 'APIKEY', 'SECRET', Iyzico::SANDBOX);
check(Iyzico::price('50.00') === '50' && Iyzico::price('10.510') === '10.51' && Iyzico::price(10.5105) === '10.5105' && Iyzico::kurusToPrice(499000) === '4990' && Iyzico::kurusToPrice(499050) === '4990.5', 'iyzico fiyat biçimi');
$order = $orders->create($plan, 'iyzico', $form + ['tax_number' => ''], null, '81.2.3.4');
$http->respond = function (string $url, array $h, $body) use ($order) {
    $token = 'tok-iyz';
    return [200, json_encode(['status' => 'success', 'token' => $token, 'conversationId' => $order['id'], 'paymentPageUrl' => 'https://sandbox-cpp.iyzipay.com?token=' . $token,
        'signature' => hash_hmac('sha256', $order['id'] . ':' . $token, 'SECRET')])];
};
$url = $iyz->initialize($order, $plan, 'https://site/geri', '11111111111');
check(str_starts_with($url, 'https://sandbox-cpp.iyzipay.com'), 'iyzico ödeme sayfası adresi');
[$callUrl, $headers, $body] = $http->calls[0];
check($callUrl === 'https://sandbox-api.iyzipay.com/payment/iyzipos/checkoutform/initialize/auth/ecom', 'iyzico uç noktası');
$decoded = base64_decode(substr($headers['Authorization'], strlen('IYZWSv2 ')));
preg_match('/^apiKey:APIKEY&randomKey:([^&]+)&signature:([0-9a-f]{64})$/', $decoded, $m);
check($m && $m[2] === bin2hex(hash_hmac('sha256', $m[1] . '/payment/iyzipos/checkoutform/initialize/auth/ecom' . $body, 'SECRET', true)), 'IYZWSv2 imzası belgedeki formülle');
$req = json_decode($body, true);
check($req['price'] === '4990' && $req['paidPrice'] === '4990' && $req['basketId'] === $order['id'] && $req['basketItems'][0]['itemType'] === 'VIRTUAL' && $req['buyer']['gsmNumber'] === '+905321234567', 'iyzico istek alanları');

$result = static function (string $status, string $price, string $basketId, string $secret = 'SECRET', string $fraud = '1') {
    $r = ['status' => 'success', 'paymentStatus' => $status, 'paymentId' => '9876', 'currency' => 'TRY', 'basketId' => $basketId, 'conversationId' => $basketId,
        'paidPrice' => (float) $price, 'price' => (float) $price, 'token' => 'tok-iyz', 'fraudStatus' => (int) $fraud];
    $r['signature'] = hash_hmac('sha256', implode(':', [$status, '9876', 'TRY', $basketId, $basketId, Iyzico::price($price), Iyzico::price($price), 'tok-iyz']), $secret);
    return [200, json_encode($r)];
};
$http->respond = fn () => $result('SUCCESS', '4990.00', $order['id'], 'BASKA');
try {
    $iyz->retrieve('tok-iyz');
    check(false, 'yanlış imza reddedilmeli');
} catch (Error $e) {
    check($e->errorCode === 'pay.iyzico_signature', 'iyzico sahte sonuç reddedildi');
}
$http->respond = fn () => $result('FAILURE', '4990.00', $order['id']);
check($iyz->retrieve('tok-iyz')['paid'] === false, 'başarısız ödeme');
$http->respond = fn () => $result('SUCCESS', '4990.00', $order['id'], 'SECRET', '0');
check($iyz->retrieve('tok-iyz')['paid'] === false, 'fraud incelemesindeki ödeme lisans almaz');
$http->respond = fn () => $result('SUCCESS', '4990.00', $order['id']);
$res = $iyz->retrieve('tok-iyz');
check($res['paid'] && $res['priceKurus'] === 499000 && $res['orderId'] === $order['id'], 'iyzico sonucu doğrulandı');
$paid = $orders->paid($res['orderId'], 'iyzico', $res['paymentId'], $res['priceKurus'], $res['currency'], $plan);
check(count($paid['licenses']) === 1, 'iyzico ödemesi → lisans');
try {
    $orders->paid($res['orderId'], 'paytr', 'x', 499000, 'TRY', $plan);
    check(false, 'başka sağlayıcının onayı kabul edilmemeli');
} catch (Error $e) {
    check($e->errorCode === 'order.provider', 'sağlayıcı uyuşmazlığı');
}

// ---------------- Yenileme ve iade
$key = $issuer->keyFor($paid['licenses'][0]['id']);
$before = (int) $paid['licenses'][0]['paid_until'];
$ro = $orders->create($renewal, 'paytr', $form, strtolower($key), '81.2.3.4');
$rr = $orders->paid($ro['id'], 'paytr', 'p', 399000, 'TRY', $renewal);
check(count($rr['renewed']) === 1 && (int) $rr['renewed'][0]['paid_until'] === $before + 365 * 86400, 'yenileme siparişi süre ekler');
$orders->refunded($ro['id'], 'yönetici');
check((int) $issuer->license($paid['licenses'][0]['id'])['paid_until'] === $before && $orders->find($ro['id'])['status'] === 'refunded', 'yenileme iadesi');
try {
    $orders->create($renewal, 'paytr', $form, 'VRNK-00000-00000-00000-00000', '81.2.3.4');
    check(false, 'bilinmeyen anahtarla yenileme reddedilmeli');
} catch (Error $e) {
    check(str_contains($e->getMessage(), 'Yenilenecek lisans'), 'bilinmeyen anahtar');
}
$orders->refunded($order['id'], 'yönetici');
check($issuer->license($paid['licenses'][0]['id'])['status'] === 'cancelled', 'satış iadesi lisansı bitirir');
$csv = $orders->csv($now - 86400, $now + 86400);
check(str_contains($csv, 'Işıl Güzellik Ltd.') && str_contains($csv, '4990,00') && str_contains($csv, 'İade'), 'muhasebe CSV');

// ---------------- Dış sistem satış bağlantısı
$secret = str_repeat('s', 40);
$ext = new ExternalSales($issuer, $secret);
$body = json_encode(['saleId' => 'shop-1001', 'kind' => 'new', 'customerName' => 'Dış Müşteri', 'email' => 'dis@example.com', 'edition' => 'Pro', 'devices' => 3, 'days' => 30]);
[$st] = $ext->handle('v1/sales', 't=' . $now . ',v1=' . str_repeat('0', 64), $body);
check($st === 401, 'imzasız dış satış reddedildi');
[$st] = $ext->handle('v1/sales', ExternalSales::sign($secret, $body, $now - 600), $body);
check($st === 401, 'eski imza reddedildi');
[$st, $a] = $ext->handle('v1/sales', ExternalSales::sign($secret, $body, $now), $body);
check($st === 200 && Keys::normalize($a['licenseKey']) !== null && $a['devices'] === 3, 'dış satış → lisans');
[, $b] = $ext->handle('v1/sales', ExternalSales::sign($secret, $body, $now), $body);
check($b['licenseId'] === $a['licenseId'], 'aynı saleId tekrar → aynı lisans');
$renewBody = json_encode(['saleId' => 'shop-1002', 'kind' => 'renewal', 'licenseKey' => $a['licenseKey'], 'days' => 30]);
[$st, $c] = $ext->handle('v1/sales', ExternalSales::sign($secret, $renewBody, $now), $renewBody);
check($st === 200 && $c['licenseId'] === $a['licenseId'] && $c['paidUntilUtc'] === Issuer::iso($now + 60 * 86400), 'dış yenileme');
$revoke = json_encode(['saleId' => 'shop-1001']);
[$st, $d] = $ext->handle('v1/sales/revoke', ExternalSales::sign($secret, $revoke, $now), $revoke);
check($st === 200 && $d['revoked'] === 1 && $issuer->license($a['licenseId'])['status'] === 'cancelled', 'dış iade');
[$st] = $ext->handle('v1/sales', ExternalSales::sign($secret, $body, $now), $body);
check($st === 409, 'iade edilmiş satış yeniden lisans üretmez');

// ---------------- Lisanslarım bağlantısı
$secretKey = 'gizli-anahtar-xxxxxxxxxxxxxxxxxxxxxxxx';
$link = MagicLink::create($secretKey, 'Isil@Example.com', $now);
check(MagicLink::verify($secretKey, $link, $now + 60) === 'isil@example.com', 'giriş bağlantısı geçerli');
check(MagicLink::verify($secretKey, $link, $now + 3600) === null, 'süresi geçmiş bağlantı');
check(MagicLink::verify($secretKey, str_replace('.', '.1', $link), $now) === null && MagicLink::verify('baska', $link, $now) === null, 'değiştirilmiş bağlantı');

echo $failures === 0 ? "OK ($count kontrol)\n" : "$failures/$count BAŞARISIZ\n";
exit($failures === 0 ? 0 : 1);
