<?php
/**
 * Kendi satış sistemi (WooCommerce olmadan) ve dış sistemler için satış bağlantısı — WordPress'ten bağımsız.
 * Ödeme sağlayıcıları (iyzico, PayTR) src/Payments.php içindedir; burada yalnızca siparişin yaşam döngüsü var:
 * bekliyor → ödendi (tutar sağlayıcıdan doğrulanınca, tam bir kez lisans) → iade.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

namespace Vernuko\License;

final class Validate
{
    public static function email(string $email): bool
    {
        return filter_var($email, FILTER_VALIDATE_EMAIL) !== false;
    }

    /** T.C. kimlik numarası (11 hane, iki kontrol hanesi). */
    public static function tckn(string $v): bool
    {
        if (preg_match('/^[1-9][0-9]{10}$/', $v) !== 1) {
            return false;
        }
        $d = array_map('intval', str_split($v));
        $tenth = ((($d[0] + $d[2] + $d[4] + $d[6] + $d[8]) * 7 - ($d[1] + $d[3] + $d[5] + $d[7])) % 10 + 10) % 10;
        return $tenth === $d[9] && array_sum(array_slice($d, 0, 10)) % 10 === $d[10];
    }

    /** Vergi kimlik numarası (10 hane), masaüstündeki TaxNumberValidator ile aynı algoritma. */
    public static function vkn(string $v): bool
    {
        if (preg_match('/^[0-9]{10}$/', $v) !== 1) {
            return false;
        }
        $sum = 0;
        for ($i = 0; $i < 9; $i++) {
            $t = ((int) $v[$i] + 9 - $i) % 10;
            $x = ($t * (1 << (9 - $i))) % 9;
            $sum += ($t !== 0 && $x === 0) ? 9 : $x;
        }
        return (10 - ($sum % 10)) % 10 === (int) $v[9];
    }

    /** Turkish mobile/landline → +90XXXXXXXXXX, or null. */
    public static function phone(string $v): ?string
    {
        $digits = preg_replace('/\D+/', '', $v);
        if (str_starts_with($digits, '90') && strlen($digits) === 12) {
            $digits = substr($digits, 2);
        } elseif (str_starts_with($digits, '0') && strlen($digits) === 11) {
            $digits = substr($digits, 1);
        }
        return strlen($digits) === 10 && $digits[0] !== '0' ? '+90' . $digits : null;
    }
}

/** A plan sold on the site: price in kuruş (TRY). */
final class Plan
{
    public function __construct(
        public readonly string $id,
        public readonly string $name,
        public readonly int $priceKurus,
        public readonly string $kind = 'new',
        public readonly string $edition = 'Standart',
        public readonly int $devices = 1,
        public readonly int $days = 365,
    ) {
    }

    /** @param array<string,mixed> $a */
    public static function fromArray(string $id, array $a): self
    {
        return new self($id, (string) ($a['name'] ?? $id), max(0, (int) ($a['price_kurus'] ?? 0)), ($a['kind'] ?? 'new') === 'renewal' ? 'renewal' : 'new',
            (string) ($a['edition'] ?? 'Standart') ?: 'Standart', max(1, min(50, (int) ($a['devices'] ?? 1))), max(1, (int) ($a['days'] ?? 365)));
    }

    public function toArray(): array
    {
        return ['name' => $this->name, 'price_kurus' => $this->priceKurus, 'kind' => $this->kind, 'edition' => $this->edition, 'devices' => $this->devices, 'days' => $this->days];
    }

    public function priceText(): string
    {
        return number_format($this->priceKurus / 100, 2, ',', '.') . ' TL';
    }

    /**
     * Turkish price input → kuruş, or null. "4.990,00", "4990", "4.990" (thousands), "49,90"; a single dot followed by
     * one or two digits is the decimal point ("49.90" — it used to become 4.990,00 TL). Anything else is rejected.
     */
    public static function parsePrice(string $text): ?int
    {
        $t = str_replace([' ', "\u{00A0}", 'TL', 'tl', '₺'], '', trim($text));
        if (preg_match('/^(\d+|\d{1,3}(\.\d{3})+)(,\d{1,2})?$/', $t) === 1) {
            $t = str_replace(['.', ','], ['', '.'], $t);
        } elseif (preg_match('/^\d+\.\d{1,2}$/', $t) !== 1) {
            return null;
        }
        return (int) round(((float) $t) * 100);
    }
}

final class Orders
{
    public function __construct(private readonly Db $db, private readonly Issuer $issuer)
    {
    }

    /** Order ids are letters and digits only (PayTR's merchant_oid rule). */
    public static function newId(): string
    {
        return 'VK' . strtoupper(bin2hex(random_bytes(10)));
    }

    /**
     * Validates the checkout form and stores a pending order. $renewLicenseKey: for renewals, the customer's key.
     * @param array<string,string> $f name, email, phone, company, tax_office, tax_number, address, city
     */
    public function create(Plan $plan, string $provider, array $f, ?string $renewLicenseKey, string $ip): array
    {
        $name = trim($f['name'] ?? '');
        $email = strtolower(trim($f['email'] ?? ''));
        $phone = Validate::phone($f['phone'] ?? '');
        $tax = preg_replace('/\s+/', '', $f['tax_number'] ?? '');
        $errors = [];
        if (mb_strlen($name) < 3 || !str_contains($name, ' ')) {
            $errors[] = 'Ad ve soyadınızı yazın.';
        }
        if (!Validate::email($email)) {
            $errors[] = 'Geçerli bir e-posta adresi yazın; lisans anahtarı buraya gönderilir.';
        }
        if ($phone === null) {
            $errors[] = 'Cep telefonu numaranızı yazın (ör. 0532 123 45 67).';
        }
        if (trim($f['address'] ?? '') === '' || trim($f['city'] ?? '') === '') {
            $errors[] = 'Fatura adresi ve şehir gerekli.';
        }
        if ($tax !== '' && !(strlen($tax) === 10 ? Validate::vkn($tax) : Validate::tckn($tax))) {
            $errors[] = 'Vergi numarası geçersiz: firma için 10 haneli VKN, şahıs için 11 haneli T.C. kimlik numarası.';
        }
        if ($plan->priceKurus <= 0) {
            $errors[] = 'Bu paketin fiyatı tanımlı değil.';
        }
        $renewId = null;
        if ($plan->kind === 'renewal') {
            $normalized = Keys::normalize($renewLicenseKey);
            $license = $normalized === null ? null : ($this->db->rows('SELECT id, status FROM {licenses} WHERE key_hash = ?', [Keys::hash($normalized)])[0] ?? null);
            if ($license === null) {
                $errors[] = 'Yenilenecek lisans anahtarı bulunamadı.';
            } else {
                $renewId = $license['id'];
            }
        }
        if ($errors) {
            throw new Error(400, 'order.invalid', implode(' ', $errors));
        }

        $order = [
            'id' => self::newId(), 'plan_id' => $plan->id, 'kind' => $plan->kind, 'renew_license_id' => $renewId, 'amount' => $plan->priceKurus,
            'currency' => 'TRY', 'provider' => $provider, 'status' => 'pending', 'customer_name' => mb_substr($name, 0, 200), 'email' => mb_substr($email, 0, 200),
            'phone' => $phone, 'company' => self::opt($f['company'] ?? '', 200), 'tax_office' => self::opt($f['tax_office'] ?? '', 100), 'tax_number' => $tax ?: null,
            'address' => self::opt($f['address'] ?? '', 500), 'city' => self::opt($f['city'] ?? '', 100), 'ip' => mb_substr($ip, 0, 64), 'created_at' => $this->issuer->now(),
            // What was bought (devices, days, edition): a plan edited or deleted while the payment page was open must
            // neither change nor block what this order gets.
            'plan_snapshot' => json_encode($plan->toArray(), JSON_UNESCAPED_UNICODE),
        ];
        $this->db->execute('INSERT INTO {orders} (' . implode(',', array_keys($order)) . ') VALUES (' . implode(',', array_fill(0, count($order), '?')) . ')', array_values($order));
        return $this->find($order['id']);
    }

    /** The plan as it was when the order was placed (older orders: the current definition, if it still exists). */
    public static function planFor(array $order, array $plans): ?Plan
    {
        $snapshot = is_string($order['plan_snapshot'] ?? null) ? json_decode($order['plan_snapshot'], true) : null;
        if (is_array($snapshot)) {
            return Plan::fromArray((string) $order['plan_id'], $snapshot);
        }
        return $plans[$order['plan_id']] ?? null;
    }

    /** iyzico's checkout token: lets the site ask iyzico later if the customer never came back to the site. */
    public function setProviderToken(string $id, string $token): void
    {
        $this->db->execute('UPDATE {orders} SET provider_token = ? WHERE id = ?', [mb_substr($token, 0, 200), $id]);
    }

    public function find(string $id): array
    {
        $row = $this->db->rows('SELECT * FROM {orders} WHERE id = ?', [$id])[0] ?? null;
        if ($row === null) {
            throw new Error(404, 'order.not_found', 'Sipariş bulunamadı.');
        }
        return $row;
    }

    /**
     * Payment confirmed BY THE PROVIDER (never by the browser): the paid amount must equal the order amount.
     * Safe to call again for the same order (provider retries, page reloads): licenses are granted once.
     * @return array{order:array,granted:bool,licenses:array,renewed:array} granted: true only the first time (send the e-mail then)
     */
    public function paid(string $id, string $provider, string $providerRef, int $paidKurus, string $currency, Plan $plan): array
    {
        $result = $this->db->transaction(function () use ($id, $provider, $providerRef, $paidKurus, $currency, $plan) {
            $order = $this->find($id);
            if ($order['provider'] !== $provider) {
                throw new Error(400, 'order.provider', 'Ödeme sağlayıcısı siparişle uyuşmuyor.');
            }
            if ($order['status'] === 'refunded') {
                return $order;
            }
            // Installments may add interest paid by the customer (more than the price); less is never accepted.
            if ($paidKurus < (int) $order['amount'] || strtoupper($currency) !== $order['currency']) {
                return null; // recorded below, outside the transaction
            }
            if ($order['status'] !== 'paid') {
                $this->db->execute("UPDATE {orders} SET status = 'paid', provider_ref = ?, paid_at = ? WHERE id = ?", [$providerRef, $this->issuer->now(), $id]);
            }
            $name = $order['company'] ?: $order['customer_name'];
            $granted = $this->issuer->grant("own:$id", "own:$id", null, $order['kind'], $plan->edition, $plan->devices, $plan->days, 1, $order['renew_license_id'], $name, $order['email'], null, 'own') !== null;
            return $this->find($id) + ['_granted' => $granted];
        });

        if ($result === null) {
            $order = $this->find($id);
            $this->db->execute("UPDATE {orders} SET status = 'mismatch', provider_ref = ? WHERE id = ? AND status = 'pending'", [$providerRef, $id]);
            $this->issuer->audit('order.amount_mismatch', null, "$id: beklenen {$order['amount']} {$order['currency']}, gelen $paidKurus $currency");
            throw new Error(409, 'order.amount', 'Ödenen tutar siparişle uyuşmuyor; lisans oluşturulmadı.');
        }

        $granted = (bool) ($result['_granted'] ?? false);
        unset($result['_granted']);
        return ['order' => $result, 'granted' => $granted, 'licenses' => $this->issuer->orderLicenses("own:$id"), 'renewed' => $this->issuer->orderRenewals("own:$id")];
    }

    public function failed(string $id, string $reason): void
    {
        $this->db->execute("UPDATE {orders} SET status = 'failed' WHERE id = ? AND status = 'pending'", [$id]);
        $this->issuer->audit('order.failed', null, "$id: " . mb_substr($reason, 0, 300));
    }

    /** The money was returned in the provider's panel: stop the license (data on the customer's PC stays). */
    public function refunded(string $id, string $by): void
    {
        $this->db->transaction(function () use ($id, $by) {
            $order = $this->find($id);
            if ($order['status'] !== 'paid') {
                throw new Error(400, 'order.not_paid', 'Yalnızca ödenmiş sipariş iade edilebilir.');
            }
            $this->issuer->revokeOrder("own:$id", "Sipariş $id iade ($by)");
            $this->db->execute("UPDATE {orders} SET status = 'refunded', refunded_at = ? WHERE id = ?", [$this->issuer->now(), $id]);
        });
    }

    public function list(?string $search = null, int $limit = 200): array
    {
        if ($search !== null && trim($search) !== '') {
            $like = '%' . trim($search) . '%';
            return $this->db->rows('SELECT * FROM {orders} WHERE id = ? OR email LIKE ? OR customer_name LIKE ? OR company LIKE ? ORDER BY created_at DESC LIMIT ' . max(1, min($limit, 1000)), [trim($search), $like, $like, $like]);
        }
        return $this->db->rows('SELECT * FROM {orders} ORDER BY created_at DESC LIMIT ' . max(1, min($limit, 1000)));
    }

    /**
     * Paid/refunded orders as CSV for the accountant (invoices are issued outside this plugin). Dates are in the
     * site's time zone ($zone): a sale at 01:30 on the 1st (Istanbul) belongs to that month, not the previous one.
     */
    public function csv(int $fromUnix, int $toUnix, ?\DateTimeZone $zone = null): string
    {
        $zone ??= new \DateTimeZone('UTC');
        $rows = $this->db->rows("SELECT * FROM {orders} WHERE status IN ('paid','refunded') AND paid_at >= ? AND paid_at < ? ORDER BY paid_at", [$fromUnix, $toUnix]);
        $out = "\u{FEFF}Sipariş;Tarih;Durum;Paket;Tutar (TL);Ödeme;Ödeme no;Ad soyad;Firma;Vergi dairesi;Vergi no;E-posta;Telefon;Adres;Şehir\n";
        foreach ($rows as $r) {
            $date = (new \DateTimeImmutable('@' . (int) $r['paid_at']))->setTimezone($zone)->format('d.m.Y H:i');
            $cells = [$r['id'], $date, $r['status'] === 'paid' ? 'Ödendi' : 'İade', $r['plan_id'], number_format($r['amount'] / 100, 2, ',', ''), $r['provider'],
                $r['provider_ref'], $r['customer_name'], $r['company'], $r['tax_office'], $r['tax_number'], $r['email'], self::nationalPhone((string) $r['phone']), $r['address'], $r['city']];
            $out .= implode(';', array_map(static fn ($c) => '"' . str_replace('"', '""', self::spreadsheetSafe((string) $c)) . '"', $cells)) . "\n";
        }
        return $out;
    }

    /** "+905321234567" → "05321234567": what the accountant expects, and no leading "+" for Excel to read as a formula. */
    private static function nationalPhone(string $e164): string
    {
        return str_starts_with($e164, '+90') ? '0' . substr($e164, 3) : $e164;
    }

    /** Customer text starting with = + - @ (or a tab/CR) would run as a formula in Excel; a leading ' keeps it text. */
    public static function spreadsheetSafe(string $value): string
    {
        return $value !== '' && str_contains("=+-@\t\r", $value[0]) ? "'" . $value : $value;
    }

    private static function opt(string $v, int $max): ?string
    {
        $v = trim($v);
        return $v === '' ? null : mb_substr($v, 0, $max);
    }
}

/**
 * Another sales system (your own site/app) tells the license server about sales:
 *   POST …/v1/sales          {"saleId","kind":"new"|"renewal","customerName","email","edition","devices","days","licenseKey"?}
 *   POST …/v1/sales/revoke   {"saleId"}
 * Header X-Vernuko-Signature: t=<unix seconds>,v1=<hex HMAC-SHA256(secret, t + "." + raw body)>; ±5 minutes.
 * The same saleId is processed once (retries are safe). The secret never leaves the two servers.
 */
final class ExternalSales
{
    public const TOLERANCE = 300;

    public function __construct(private readonly Issuer $issuer, private readonly string $secret)
    {
    }

    public static function sign(string $secret, string $body, int $timestamp): string
    {
        return "t=$timestamp,v1=" . hash_hmac('sha256', $timestamp . '.' . $body, $secret);
    }

    public function verify(?string $header, string $body): bool
    {
        if ($header === null || strlen($this->secret) < 32 || preg_match('/^t=(\d{9,11}),v1=([0-9a-f]{64})$/', trim($header), $m) !== 1) {
            return false;
        }
        if (abs($this->issuer->now() - (int) $m[1]) > self::TOLERANCE) {
            return false;
        }
        return hash_equals(hash_hmac('sha256', $m[1] . '.' . $body, $this->secret), $m[2]);
    }

    /** @return array{0:int,1:array} */
    public function handle(string $route, ?string $signature, string $rawBody): array
    {
        if (!$this->verify($signature, $rawBody)) {
            return [401, ['code' => 'sales.signature', 'message' => 'İmza geçersiz veya süresi geçmiş.']];
        }
        $body = json_decode($rawBody, true);
        $saleId = is_array($body) && is_string($body['saleId'] ?? null) ? $body['saleId'] : '';
        if (preg_match('/^[A-Za-z0-9._:-]{1,60}$/', $saleId) !== 1) {
            return [400, ['code' => 'sales.sale_id', 'message' => 'saleId gerekli (harf, rakam, . _ : -; en fazla 60).']];
        }
        try {
            if ($route === 'v1/sales/revoke') {
                return [200, ['saleId' => $saleId, 'revoked' => $this->issuer->revokeOrder("ext:$saleId", "Dış satış $saleId iade")]];
            }
            if ($route !== 'v1/sales') {
                return [404, ['code' => 'request.unknown', 'message' => 'Bilinmeyen işlem.']];
            }
            $kind = ($body['kind'] ?? 'new') === 'renewal' ? 'renewal' : 'new';
            $renewId = null;
            if ($kind === 'renewal') {
                $normalized = Keys::normalize(is_string($body['licenseKey'] ?? null) ? $body['licenseKey'] : null);
                $renewId = $normalized === null ? null : ($this->issuer->licenseByKeyHash(Keys::hash($normalized))['id'] ?? null);
                if ($renewId === null) {
                    return [404, ['code' => 'license.not_found', 'message' => 'Yenilenecek lisans anahtarı bulunamadı.']];
                }
            }
            $email = is_string($body['email'] ?? null) && Validate::email($body['email']) ? strtolower($body['email']) : null;
            $this->issuer->grant("ext:$saleId", "ext:$saleId", null, $kind, (string) ($body['edition'] ?? 'Standart') ?: 'Standart', (int) ($body['devices'] ?? 1),
                (int) ($body['days'] ?? 365), 1, $renewId, (string) ($body['customerName'] ?? ''), $email, null, 'external');
            $licenses = $kind === 'renewal' ? $this->issuer->orderRenewals("ext:$saleId") : $this->issuer->orderLicenses("ext:$saleId");
            if (!$licenses) {
                return [409, ['code' => 'sales.revoked', 'message' => 'Bu satış daha önce iade edilmiş.']];
            }
            $l = $licenses[0];
            return [200, ['saleId' => $saleId, 'licenseId' => $l['id'], 'licenseKey' => $this->issuer->keyFor($l['id']), 'paidUntilUtc' => Issuer::iso((int) $l['paid_until']), 'devices' => (int) $l['max_devices']]];
        } catch (Error $e) {
            return [$e->status, ['code' => $e->errorCode, 'message' => $e->getMessage()]];
        }
    }
}

/** "Lisanslarım" without accounts: a sign-in link sent to the e-mail address (30 minutes). */
final class MagicLink
{
    public static function create(string $secret, string $email, int $now, int $ttl = 1800): string
    {
        $email = strtolower(trim($email));
        $exp = $now + $ttl;
        return Signer::b64url($email) . '.' . $exp . '.' . hash_hmac('sha256', "magic|$email|$exp", $secret);
    }

    /** The e-mail address, or null when the link is wrong or expired. */
    public static function verify(string $secret, string $token, int $now): ?string
    {
        $parts = explode('.', $token);
        if (count($parts) !== 3 || !ctype_digit($parts[1]) || (int) $parts[1] < $now) {
            return null;
        }
        $email = base64_decode(strtr($parts[0], '-_', '+/'), true);
        if ($email === false || !Validate::email($email)) {
            return null;
        }
        return hash_equals(hash_hmac('sha256', "magic|$email|{$parts[1]}", $secret), $parts[2]) ? $email : null;
    }
}
