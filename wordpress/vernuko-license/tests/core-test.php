<?php
/** Çekirdek testleri (PHPUnit gerektirmez): php tests/core-test.php */

require __DIR__ . '/../src/Core.php';

use Vernuko\License\Api;
use Vernuko\License\Error;
use Vernuko\License\Issuer;
use Vernuko\License\Keys;
use Vernuko\License\PdoDb;
use Vernuko\License\Signer;

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

function expectError(callable $f, string $code, string $what): void
{
    try {
        $f();
        check(false, "$what (hata bekleniyordu: $code)");
    } catch (Error $e) {
        check($e->errorCode === $code, "$what: beklenen $code, gelen {$e->errorCode}");
    }
}

[$pem, $public] = Signer::generate();
$signer = Signer::fromPem($pem, 'k1');
$now = 1794380400; // 2026-11-12T07:00:00Z
$clock = static function () use (&$now): int {
    return $now;
};
$db = new PdoDb(new PDO('sqlite::memory:'));
$db->createSchema();
$issuer = new Issuer($db, $signer, random_bytes(32), clock: $clock);
$api = new Api($issuer);
$devA = '0123456789abcdef0123456789abcdef';
$devB = 'fedcba9876543210fedcba9876543210';

// Anahtar biçimi
$k = Keys::derive('secret', 'lic_x');
check(preg_match('/^VRNK(-[0-9A-HJKMNP-TV-Z]{5}){4}$/', $k) === 1, "anahtar biçimi $k");
check(Keys::normalize(strtolower(str_replace('-', ' ', $k))) === Keys::normalize($k), 'normalize küçük harf/boşluk');
check(Keys::normalize('VRNK-123') === null, 'kısa anahtar geçersiz');

// İmza: DER → r||s ve OpenSSL ile doğrulama (r||s → DER geri çevirmeden, açık anahtarla payload doğrulanır)
$signed = json_decode($signer->sign(['v' => 1, 'x' => 'ğüşıöç']), true);
check($signed['alg'] === 'ES256' && $signed['kid'] === 'k1', 'zarf alanları');
$raw = base64_decode(strtr($signed['sig'], '-_', '+/'));
check(strlen($raw) === 64, 'imza 64 bayt (r||s)');
$r = ltrim(substr($raw, 0, 32), "\x00");
$s = ltrim(substr($raw, 32), "\x00");
$int = static fn (string $v) => "\x02" . chr(strlen((ord($v[0]) & 0x80) ? "\x00$v" : $v)) . ((ord($v[0]) & 0x80) ? "\x00$v" : $v);
$der = $int($r) . $int($s);
$der = "\x30" . chr(strlen($der)) . $der;
$pub = openssl_pkey_get_public("-----BEGIN PUBLIC KEY-----\n" . chunk_split($public, 64, "\n") . "-----END PUBLIC KEY-----\n");
check(openssl_verify($signed['payload'], $der, $pub, OPENSSL_ALGO_SHA256) === 1, 'imza açık anahtarla doğrulanıyor');

// Etkinleştirme, koltuk, taşıma
$lic = $issuer->createLicense('Işıl Güzellik', 'isil@example.com', 7, 'Standart', 1, 365, 'manual');
[$st, $a] = $api->handle('v1/activate', ['licenseKey' => strtolower($lic['key']), 'deviceHash' => $devA]);
check($st === 200 && isset($a['license'], $a['activationId']), 'etkinleştirme 200');
$doc = json_decode(base64_decode(strtr(json_decode($a['license'], true)['payload'], '-_', '+/')), true);
check($doc['exp'] === Issuer::iso($now + 372 * 86400), 'exp = ödenen dönem + 7 gün tolerans: ' . $doc['exp']);
check($doc['rfr'] === Issuer::iso($now + 7 * 86400) && $doc['dev'] === $devA && $doc['grace'] === 14 && $doc['feat'] === [], 'rfr/dev/grace/feat');
[, $again] = $api->handle('v1/activate', ['licenseKey' => $lic['key'], 'deviceHash' => $devA]);
check($again['activationId'] === $a['activationId'], 'aynı bilgisayar yeni koltuk harcamaz');
[$st] = $api->handle('v1/activate', ['licenseKey' => $lic['key'], 'deviceHash' => $devB]);
check($st === 409, 'ikinci bilgisayar 409');
[$st] = $api->handle('v1/deactivate', ['activationId' => $a['activationId'], 'deviceHash' => $devA]);
check($st === 200, 'taşıma 200');
[$st, $b] = $api->handle('v1/activate', ['licenseKey' => $lic['key'], 'deviceHash' => $devB]);
check($st === 200, 'taşımadan sonra yeni bilgisayar');
[$st, $e] = $api->handle('v1/refresh', ['activationId' => $a['activationId'], 'deviceHash' => $devA]);
check($st === 403 && $e['code'] === 'activation.removed', 'eski bilgisayar yenileyemez');
[$st] = $api->handle('v1/refresh', ['activationId' => $b['activationId'], 'deviceHash' => $devA]);
check($st === 404, 'başkasının etkinleştirme kimliği kullanılamaz');
[$st, $e] = $api->handle('v1/activate', ['licenseKey' => 'VRNK-00000-00000-00000-00000', 'deviceHash' => $devA]);
check($st === 404 && $e['code'] === 'license.not_found', 'yanlış anahtar 404');
[$st] = $api->handle('v1/activate', ['licenseKey' => 'abc', 'deviceHash' => $devA]);
check($st === 400, 'bozuk anahtar 400');

// Yenileme satın alma: dönem sonundan eklenir; iade edilen yenileme geri alınır
$before = (int) $issuer->license($lic['id'])['paid_until'];
$issuer->extend($lic['id'], 365, 'test');
check((int) $issuer->license($lic['id'])['paid_until'] === $before + 365 * 86400, 'yenileme dönem sonuna eklenir');
$issuer->shorten($lic['id'], 365, 'iade');
check((int) $issuer->license($lic['id'])['paid_until'] === $before, 'iade edilen yenileme geri alınır');

// İptal: ödenen dönem korunur (tolerans yok); iade: hemen biter
$issuer->cancel($lic['id'], false, 'test');
$now += 10 * 86400;
[$st, $r2] = $api->handle('v1/refresh', ['activationId' => $b['activationId'], 'deviceHash' => $devB]);
$doc = json_decode(base64_decode(strtr(json_decode($r2['license'], true)['payload'], '-_', '+/')), true);
check($st === 200 && $doc['exp'] === Issuer::iso($before), 'iptalde exp = ödenen dönem sonu');
$issuer->cancel($lic['id'], true, 'iade');
[$st, $e] = $api->handle('v1/refresh', ['activationId' => $b['activationId'], 'deviceHash' => $devB]);
check($st === 403 && $e['code'] === 'license.ended' && str_contains($e['message'], 'silinmez'), 'iadeden sonra yenileme yok');

// Deneme kaydı
[, $t1] = $api->handle('v1/trial', ['deviceHash' => $devA]);
$now += 86400;
[, $t2] = $api->handle('v1/trial', ['deviceHash' => $devA]);
check($t1['startedAtUtc'] === $t2['startedAtUtc'] && $t1['trialDays'] === 30, 'deneme başlangıcı sabit');

// Güncelleme dosyaları
$dir = sys_get_temp_dir() . '/vk-rel-' . bin2hex(random_bytes(4));
mkdir($dir);
file_put_contents("$dir/releases.win.json", '{}');
check(Api::safeReleaseFile($dir, 'releases.win.json') !== null, 'güncelleme dosyası');
check(Api::safeReleaseFile($dir, '../etc/passwd') === null && Api::safeReleaseFile($dir, '.htaccess') === null, 'yol atlatma yok');

expectError(fn () => $issuer->createLicense('', null, null, 'Standart', 1, 365, 'manual'), 'request.invalid', 'boş müşteri adı');

// Sipariş kalemi → lisans: tam bir kez (aynı kalem ikinci kez işlenmez), iade geri alır
$note = $issuer->grantOrderItem(100, 501, 'new', 'Standart', 2, 365, 2, null, 'Sipariş Müşterisi', 'siparis@example.com', 9);
check($note !== null && count($issuer->orderLicenses(100)) === 2, 'adet 2 → 2 lisans');
check($issuer->grantOrderItem(100, 501, 'new', 'Standart', 2, 365, 2, null, 'Sipariş Müşterisi', 'siparis@example.com', 9) === null, 'aynı kalem ikinci kez lisans üretmez');
check(count($issuer->licenses('siparis@example.com')) === 2, 'hâlâ 2 lisans');
$sold = $issuer->orderLicenses(100)[0];
$until = (int) $sold['paid_until'];
$issuer->grantOrderItem(101, 502, 'renewal', '', 1, 365, 1, $sold['id'], '', null, null);
check((int) $issuer->license($sold['id'])['paid_until'] === $until + 365 * 86400, 'yenileme kalemi süre ekler');
check($issuer->revokeOrder(101, 'iade') === 1 && (int) $issuer->license($sold['id'])['paid_until'] === $until, 'yenileme iadesi süreyi geri alır');
check($issuer->revokeOrder(101, 'iade') === 0, 'iade ikinci kez uygulanmaz');
$issuer->revokeOrder(100, 'iade');
check($issuer->orderLicenses(100) === [] && $issuer->license($sold['id'])['status'] === 'cancelled', 'satış iadesi lisansları bitirir');
expectError(fn () => $issuer->grantOrderItem(102, 503, 'renewal', '', 1, 365, 1, null, '', null, null), 'grant.no_license', 'lisanssız yenileme');

echo $failures === 0 ? "OK ($count kontrol)\n" : "$failures/$count BAŞARISIZ\n";
exit($failures === 0 ? 0 : 1);
