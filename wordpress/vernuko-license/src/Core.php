<?php
/**
 * Vernuko lisans çekirdeği — WordPress'ten bağımsız (testlerde SQLite/PDO ile, sitede $wpdb ile çalışır).
 *
 * Masaüstü uygulamanın beklediği biçim (src/Vernuko.Licensing): ECDSA P-256 / SHA-256 imzalı belge,
 * {"kid","alg":"ES256","payload":base64url(JSON),"sig":base64url(r||s)}; imza payload metninin ASCII baytları üzerinde.
 * Müşterinin randevu/müşteri verisi bu sisteme hiç gelmez: yalnızca lisans anahtarı, özetlenmiş cihaz kimliği ve
 * etkinleştirme kimliği.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

namespace Vernuko\License;

final class Error extends \RuntimeException
{
    public function __construct(public readonly int $status, public readonly string $errorCode, string $message)
    {
        parent::__construct($message);
    }
}

/** Lisans anahtarı: "VRNK-XXXXX-XXXXX-XXXXX-XXXXX" (100 bit), HMAC(sır, lisans kimliği) ile türetilir; saklanmaz. */
final class Keys
{
    private const ALPHABET = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';

    public static function newLicenseId(): string
    {
        return 'lic_' . bin2hex(random_bytes(10));
    }

    public static function newActivationId(): string
    {
        return 'act_' . bin2hex(random_bytes(10));
    }

    public static function derive(string $secret, string $licenseId): string
    {
        $mac = hash_hmac('sha256', $licenseId, $secret, true);
        $out = '';
        $acc = 0;
        $bits = 0;
        $i = 0;
        while (strlen($out) < 20) {
            if ($bits < 5) {
                $acc = (($acc << 8) | ord($mac[$i++])) & 0xFFFF;
                $bits += 8;
            }
            $out .= self::ALPHABET[($acc >> ($bits - 5)) & 31];
            $bits -= 5;
        }
        return 'VRNK-' . substr($out, 0, 5) . '-' . substr($out, 5, 5) . '-' . substr($out, 10, 5) . '-' . substr($out, 15, 5);
    }

    /** Büyük harf, boşluk/tire yok; O→0, I/L→1, U→V. Geçersizse null. */
    public static function normalize(?string $key): ?string
    {
        if ($key === null || trim($key) === '') {
            return null;
        }
        $s = strtr(strtoupper(trim($key)), ['O' => '0', 'I' => '1', 'L' => '1', 'U' => 'V', '-' => '', ' ' => '']);
        if (strlen($s) === 24 && str_starts_with($s, 'VRNK')) {
            $s = substr($s, 4);
        }
        if (strlen($s) !== 20 || strspn($s, self::ALPHABET) !== 20) {
            return null;
        }
        return $s;
    }

    public static function hash(string $normalized): string
    {
        return hash('sha256', $normalized);
    }
}

final class Signer
{
    public function __construct(private readonly \OpenSSLAsymmetricKey $key, public readonly string $keyId)
    {
        $details = openssl_pkey_get_details($key);
        if (($details['type'] ?? null) !== OPENSSL_KEYTYPE_EC || ($details['ec']['curve_name'] ?? '') !== 'prime256v1') {
            throw new \InvalidArgumentException('İmzalama anahtarı ECDSA P-256 olmalı.');
        }
    }

    public static function fromPem(string $pem, string $keyId): self
    {
        $key = openssl_pkey_get_private($pem);
        if ($key === false) {
            throw new \InvalidArgumentException('İmzalama anahtarı okunamadı.');
        }
        return new self($key, $keyId);
    }

    /** Yeni kurulum için: [özel anahtar PEM, açık anahtar (SubjectPublicKeyInfo DER, base64)]. */
    public static function generate(): array
    {
        $key = openssl_pkey_new(['private_key_type' => OPENSSL_KEYTYPE_EC, 'curve_name' => 'prime256v1']);
        openssl_pkey_export($key, $pem);
        $public = openssl_pkey_get_details($key)['key'];
        $der = base64_decode(preg_replace('/-----[^-]+-----|\s+/', '', $public));
        return [$pem, base64_encode($der)];
    }

    public function publicKeyBase64(): string
    {
        $public = openssl_pkey_get_details($this->key)['key'];
        return preg_replace('/-----[^-]+-----|\s+/', '', $public);
    }

    public function sign(array $document): string
    {
        $payload = self::b64url(json_encode($document, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR));
        if (!openssl_sign($payload, $der, $this->key, OPENSSL_ALGO_SHA256)) {
            throw new \RuntimeException('İmzalanamadı.');
        }
        return json_encode(['kid' => $this->keyId, 'payload' => $payload, 'sig' => self::b64url(self::derToP1363($der)), 'alg' => 'ES256'], JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR);
    }

    public static function b64url(string $data): string
    {
        return rtrim(strtr(base64_encode($data), '+/', '-_'), '=');
    }

    /** OpenSSL DER (SEQUENCE{INTEGER r, INTEGER s}) → .NET'in beklediği r||s (32+32 bayt). */
    public static function derToP1363(string $der): string
    {
        $pos = 0;
        $read = static function () use ($der, &$pos): int {
            return ord($der[$pos++]);
        };
        $length = static function () use ($read): int {
            $len = $read();
            if ($len & 0x80) {
                $n = $len & 0x7F;
                $len = 0;
                for ($i = 0; $i < $n; $i++) {
                    $len = ($len << 8) | $read();
                }
            }
            return $len;
        };
        if ($read() !== 0x30) {
            throw new \RuntimeException('Beklenmeyen imza biçimi.');
        }
        $length();
        $parts = [];
        for ($k = 0; $k < 2; $k++) {
            if ($read() !== 0x02) {
                throw new \RuntimeException('Beklenmeyen imza biçimi.');
            }
            $len = $length();
            $int = ltrim(substr($der, $pos, $len), "\x00");
            $pos += $len;
            $parts[] = str_pad($int, 32, "\x00", STR_PAD_LEFT);
        }
        return $parts[0] . $parts[1];
    }
}

/**
 * Basit veritabanı bağlantısı: "?" yer tutucularıyla sorgu. WordPress'te $wpdb, testlerde PDO (SQLite).
 * Tablo adları {licenses}, {activations}, {trials}, {audit} biçiminde yazılır ve önekle değiştirilir.
 */
interface Db
{
    public function rows(string $sql, array $params = []): array;

    public function execute(string $sql, array $params = []): int;

    public function transaction(callable $work): mixed;
}

final class PdoDb implements Db
{
    public function __construct(private readonly \PDO $pdo, private readonly string $prefix = 'vk_')
    {
        $pdo->setAttribute(\PDO::ATTR_ERRMODE, \PDO::ERRMODE_EXCEPTION);
    }

    public function rows(string $sql, array $params = []): array
    {
        $st = $this->pdo->prepare($this->tables($sql));
        $st->execute($params);
        return $st->fetchAll(\PDO::FETCH_ASSOC);
    }

    public function execute(string $sql, array $params = []): int
    {
        $st = $this->pdo->prepare($this->tables($sql));
        $st->execute($params);
        return $st->rowCount();
    }

    public function transaction(callable $work): mixed
    {
        $this->pdo->beginTransaction();
        try {
            $result = $work();
            $this->pdo->commit();
            return $result;
        } catch (\Throwable $e) {
            $this->pdo->rollBack();
            throw $e;
        }
    }

    public function createSchema(): void
    {
        foreach (Schema::sqlite() as $sql) {
            $this->pdo->exec($this->tables($sql));
        }
    }

    private function tables(string $sql): string
    {
        return preg_replace('/\{(\w+)\}/', $this->prefix . 'vernuko_$1', $sql);
    }
}

final class Schema
{
    /** @return string[] */
    public static function sqlite(): array
    {
        return [
            'CREATE TABLE IF NOT EXISTS {licenses} (id TEXT PRIMARY KEY, key_hash TEXT NOT NULL UNIQUE, customer_name TEXT NOT NULL, customer_email TEXT, user_id INTEGER, edition TEXT NOT NULL, features TEXT NOT NULL, max_devices INTEGER NOT NULL, paid_until INTEGER NOT NULL, status TEXT NOT NULL, source TEXT NOT NULL, order_id INTEGER, note TEXT, created_at INTEGER NOT NULL)',
            'CREATE TABLE IF NOT EXISTS {activations} (id TEXT PRIMARY KEY, license_id TEXT NOT NULL, device_hash TEXT NOT NULL, machine_label TEXT, created_at INTEGER NOT NULL, last_seen INTEGER NOT NULL, deactivated_at INTEGER)',
            'CREATE TABLE IF NOT EXISTS {trials} (device_hash TEXT PRIMARY KEY, started_at INTEGER NOT NULL)',
            'CREATE TABLE IF NOT EXISTS {audit} (id INTEGER PRIMARY KEY AUTOINCREMENT, at INTEGER NOT NULL, action TEXT NOT NULL, license_id TEXT, detail TEXT NOT NULL)',
            'CREATE TABLE IF NOT EXISTS {grants} (item_id INTEGER PRIMARY KEY, order_id INTEGER NOT NULL, kind TEXT NOT NULL, license_ids TEXT NOT NULL, days INTEGER NOT NULL, created_at INTEGER NOT NULL, reverted_at INTEGER)',
        ];
    }

    /** WordPress dbDelta biçimi (MySQL/MariaDB). */
    public static function mysql(string $prefix, string $charset): array
    {
        $t = static fn (string $name) => $prefix . 'vernuko_' . $name;
        return [
            "CREATE TABLE {$t('licenses')} (
  id varchar(40) NOT NULL,
  key_hash char(64) NOT NULL,
  customer_name varchar(200) NOT NULL,
  customer_email varchar(200) DEFAULT NULL,
  user_id bigint(20) unsigned DEFAULT NULL,
  edition varchar(60) NOT NULL,
  features varchar(500) NOT NULL,
  max_devices int(11) NOT NULL,
  paid_until bigint(20) NOT NULL,
  status varchar(20) NOT NULL,
  source varchar(20) NOT NULL,
  order_id bigint(20) unsigned DEFAULT NULL,
  note text,
  created_at bigint(20) NOT NULL,
  PRIMARY KEY  (id),
  UNIQUE KEY key_hash (key_hash),
  KEY user_id (user_id),
  KEY order_id (order_id)
) $charset;",
            "CREATE TABLE {$t('activations')} (
  id varchar(40) NOT NULL,
  license_id varchar(40) NOT NULL,
  device_hash varchar(128) NOT NULL,
  machine_label varchar(100) DEFAULT NULL,
  created_at bigint(20) NOT NULL,
  last_seen bigint(20) NOT NULL,
  deactivated_at bigint(20) DEFAULT NULL,
  PRIMARY KEY  (id),
  KEY license_device (license_id,device_hash)
) $charset;",
            "CREATE TABLE {$t('trials')} (
  device_hash varchar(128) NOT NULL,
  started_at bigint(20) NOT NULL,
  PRIMARY KEY  (device_hash)
) $charset;",
            "CREATE TABLE {$t('audit')} (
  id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  at bigint(20) NOT NULL,
  action varchar(60) NOT NULL,
  license_id varchar(40) DEFAULT NULL,
  detail text NOT NULL,
  PRIMARY KEY  (id),
  KEY license_id (license_id)
) $charset;",
            "CREATE TABLE {$t('grants')} (
  item_id bigint(20) unsigned NOT NULL,
  order_id bigint(20) unsigned NOT NULL,
  kind varchar(20) NOT NULL,
  license_ids text NOT NULL,
  days int(11) NOT NULL,
  created_at bigint(20) NOT NULL,
  reverted_at bigint(20) DEFAULT NULL,
  PRIMARY KEY  (item_id),
  KEY order_id (order_id)
) $charset;",
        ];
    }
}

final class Settings
{
    public function __construct(
        public readonly int $refreshDays = 7,
        public readonly int $paymentToleranceDays = 7,
        public readonly int $offlineGraceDays = 14,
        public readonly int $trialDays = 30,
    ) {
    }
}

/**
 * Lisans kuralları (.NET lisans sunucusuyla ve masaüstü değerlendiricisiyle aynı): belge geçerliliği abonelik
 * sürerken ödenen dönem + 7 gün tolerans; iptal/duraklatmada ödenen dönem sonu. Reddedilen yenileme yalnızca
 * eldeki belgenin süresinin dolmasına izin verir; müşteri verisi asla silinmez veya kilitlenmez.
 */
final class Issuer
{
    public const PRODUCT = 'vernuko-desktop';

    /** @param callable():int $clock */
    public function __construct(
        private readonly Db $db,
        private readonly Signer $signer,
        private readonly string $keySecret,
        private readonly Settings $settings = new Settings(),
        private $clock = null,
    ) {
        $this->clock ??= static fn (): int => time();
    }

    public function now(): int
    {
        return ($this->clock)();
    }

    public function activate(?string $licenseKey, ?string $deviceHash, ?string $machineLabel = null): array
    {
        $this->requireDevice($deviceHash);
        $normalized = Keys::normalize($licenseKey);
        if ($normalized === null) {
            throw new Error(400, 'license.key_format', 'Lisans anahtarı VRNK-XXXXX-XXXXX-XXXXX-XXXXX biçiminde olmalı.');
        }

        return $this->db->transaction(function () use ($normalized, $deviceHash, $machineLabel) {
            $license = $this->db->rows('SELECT * FROM {licenses} WHERE key_hash = ?', [Keys::hash($normalized)])[0] ?? null;
            if ($license === null) {
                throw new Error(404, 'license.not_found', 'Bu lisans anahtarı bulunamadı. Anahtarı kontrol edin.');
            }
            if ($this->effectiveEnd($license) <= $this->now()) {
                throw new Error(403, 'license.ended', $this->endedMessage($license));
            }

            $active = $this->db->rows('SELECT * FROM {activations} WHERE license_id = ? AND deactivated_at IS NULL', [$license['id']]);
            $activation = null;
            foreach ($active as $a) {
                if (hash_equals($a['device_hash'], $deviceHash)) {
                    $activation = $a;
                }
            }

            if ($activation === null) {
                if (count($active) >= (int) $license['max_devices']) {
                    throw new Error(409, 'license.seats', "Bu lisans {$license['max_devices']} bilgisayar içindir ve şu an başka bilgisayarda etkin. Eski bilgisayarda Ayarlar › Lisans › \"Bu bilgisayardan kaldır\" yapın veya destekle iletişime geçin.");
                }
                $activation = ['id' => Keys::newActivationId(), 'device_hash' => $deviceHash];
                $this->db->execute('INSERT INTO {activations} (id, license_id, device_hash, machine_label, created_at, last_seen) VALUES (?, ?, ?, ?, ?, ?)',
                    [$activation['id'], $license['id'], $deviceHash, $machineLabel === null ? null : mb_substr($machineLabel, 0, 100), $this->now(), $this->now()]);
                $this->audit('activation.created', $license['id'], $activation['id']);
            } else {
                $this->db->execute('UPDATE {activations} SET last_seen = ? WHERE id = ?', [$this->now(), $activation['id']]);
            }

            return ['license' => $this->sign($license, $activation['id'], $deviceHash), 'activationId' => $activation['id']];
        });
    }

    public function refresh(?string $activationId, ?string $deviceHash): array
    {
        [$license, $activation] = $this->findActivation($activationId, $deviceHash);
        if ($this->effectiveEnd($license) <= $this->now()) {
            throw new Error(403, 'license.ended', $this->endedMessage($license));
        }
        $this->db->execute('UPDATE {activations} SET last_seen = ? WHERE id = ?', [$this->now(), $activation['id']]);
        return ['license' => $this->sign($license, $activation['id'], $activation['device_hash']), 'activationId' => $activation['id']];
    }

    /** Yeni bilgisayara taşıma: koltuğu boşaltır. Masaüstü kendi lisans dosyasını siler; veriler kalır. */
    public function deactivate(?string $activationId, ?string $deviceHash): array
    {
        [$license, $activation] = $this->findActivation($activationId, $deviceHash);
        $this->db->execute('UPDATE {activations} SET deactivated_at = ? WHERE id = ?', [$this->now(), $activation['id']]);
        $this->audit('activation.deactivated', $license['id'], $activation['id'] . ' (müşteri)');
        return ['message' => 'Lisans bu bilgisayardan kaldırıldı.'];
    }

    /** Cihazın ilk deneme başlangıcı (tekrar çağrılınca aynı tarih). */
    public function trial(?string $deviceHash): array
    {
        $this->requireDevice($deviceHash);
        $row = $this->db->rows('SELECT started_at FROM {trials} WHERE device_hash = ?', [$deviceHash])[0] ?? null;
        if ($row === null) {
            $this->db->execute('INSERT INTO {trials} (device_hash, started_at) VALUES (?, ?)', [$deviceHash, $this->now()]);
            $started = $this->now();
        } else {
            $started = (int) $row['started_at'];
        }
        return ['startedAtUtc' => self::iso($started), 'trialDays' => $this->settings->trialDays];
    }

    public function updatePermission(?string $activationId, ?string $deviceHash): array
    {
        try {
            [$license] = $this->findActivation($activationId, $deviceHash);
        } catch (Error $e) {
            return ['allowed' => false, 'message' => $e->getMessage()];
        }
        return $this->effectiveEnd($license) > $this->now()
            ? ['allowed' => true, 'message' => 'Güncellemeler açık.']
            : ['allowed' => false, 'message' => 'Güncellemeler etkin bir abonelikle alınır. Mevcut sürüm ve verileriniz çalışmaya devam eder.'];
    }

    // ---------- Satış ve yönetim ----------

    /** @return array{id:string,key:string} */
    public function createLicense(string $customerName, ?string $email, ?int $userId, string $edition, int $maxDevices, int $days, string $source, ?int $orderId = null, array $features = [], ?string $note = null): array
    {
        if (trim($customerName) === '' || $maxDevices < 1 || $maxDevices > 50 || $days < 1) {
            throw new Error(400, 'request.invalid', 'Müşteri adı, 1–50 cihaz ve en az 1 gün gerekli.');
        }
        $id = Keys::newLicenseId();
        $key = Keys::derive($this->keySecret, $id);
        $this->db->execute('INSERT INTO {licenses} (id, key_hash, customer_name, customer_email, user_id, edition, features, max_devices, paid_until, status, source, order_id, note, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)', [
            $id, Keys::hash(Keys::normalize($key)), trim($customerName), $email, $userId, $edition, implode(',', $features), $maxDevices,
            $this->now() + $days * 86400, 'active', $source, $orderId, $note, $this->now(),
        ]);
        $this->audit('license.created', $id, "$source: $customerName, $edition, $maxDevices cihaz, $days gün" . ($orderId ? ", sipariş #$orderId" : ''));
        return ['id' => $id, 'key' => $key];
    }

    /** Yenileme: süre bugünden ya da mevcut dönem sonundan (hangisi ileriyse) eklenir. */
    public function extend(string $licenseId, int $days, string $reason): array
    {
        $license = $this->license($licenseId);
        $from = max($this->now(), (int) $license['paid_until']);
        $until = $from + $days * 86400;
        $this->db->execute("UPDATE {licenses} SET paid_until = ?, status = 'active' WHERE id = ?", [$until, $licenseId]);
        $this->audit('license.extended', $licenseId, "$reason: +$days gün, yeni dönem sonu " . gmdate('Y-m-d', $until));
        return $this->license($licenseId);
    }

    /** İade edilen yenileme: eklenen süre geri alınır (bugünden geriye gitmez). */
    public function shorten(string $licenseId, int $days, string $reason): void
    {
        $license = $this->license($licenseId);
        $until = max($this->now(), (int) $license['paid_until'] - $days * 86400);
        $this->db->execute('UPDATE {licenses} SET paid_until = ? WHERE id = ?', [$until, $licenseId]);
        $this->audit('license.shortened', $licenseId, "$reason: -$days gün");
    }

    /** İptal: yenilemeyi durdurur. $endNow (iade) ödenen dönemi de bitirir; aksi halde dönem sonuna kadar çalışır. */
    public function cancel(string $licenseId, bool $endNow, string $reason): void
    {
        $license = $this->license($licenseId);
        $until = $endNow ? min((int) $license['paid_until'], $this->now()) : (int) $license['paid_until'];
        $this->db->execute("UPDATE {licenses} SET status = 'cancelled', paid_until = ? WHERE id = ?", [$until, $licenseId]);
        $this->audit('license.cancelled', $licenseId, $reason);
    }

    public function removeActivation(string $activationId, string $by): void
    {
        $a = $this->db->rows('SELECT * FROM {activations} WHERE id = ?', [$activationId])[0] ?? null;
        if ($a === null) {
            throw new Error(404, 'activation.not_found', 'Cihaz bulunamadı.');
        }
        $this->db->execute('UPDATE {activations} SET deactivated_at = ? WHERE id = ? AND deactivated_at IS NULL', [$this->now(), $activationId]);
        $this->audit('activation.deactivated', $a['license_id'], "$activationId ($by)");
    }

    /**
     * One paid order line → licenses, exactly once (the order line id is the key of the grants table, so a second
     * call — another hook, a retry, a stale order object — changes nothing). Returns an order note, or null when
     * this line was already handled.
     */
    public function grantOrderItem(int $orderId, int $itemId, string $kind, string $edition, int $devices, int $days, int $qty, ?string $renewLicenseId, string $name, ?string $email, ?int $userId): ?string
    {
        return $this->db->transaction(function () use ($orderId, $itemId, $kind, $edition, $devices, $days, $qty, $renewLicenseId, $name, $email, $userId) {
            if ($this->db->rows('SELECT item_id FROM {grants} WHERE item_id = ?', [$itemId])) {
                return null;
            }
            $qty = max(1, $qty);
            if ($kind === 'renewal') {
                if ($renewLicenseId === null || $renewLicenseId === '') {
                    throw new Error(400, 'grant.no_license', 'Yenilenecek lisans seçilmemiş.');
                }
                $this->extend($renewLicenseId, $days * $qty, "Sipariş #$orderId");
                $ids = [$renewLicenseId];
                $note = 'Vernuko: lisans yenilendi (+' . ($days * $qty) . ' gün).';
            } else {
                $ids = [];
                for ($i = 0; $i < $qty; $i++) {
                    $ids[] = $this->createLicense($name, $email, $userId, $edition, $devices, $days, 'woocommerce', $orderId)['id'];
                }
                $note = 'Vernuko: ' . count($ids) . ' lisans oluşturuldu.';
            }
            $this->db->execute('INSERT INTO {grants} (item_id, order_id, kind, license_ids, days, created_at) VALUES (?, ?, ?, ?, ?, ?)',
                [$itemId, $orderId, $kind, implode(',', $ids), $days * $qty, $this->now()]);
            return $note;
        });
    }

    /** Refund/cancel: new licenses end now; renewal days are taken back. Returns how many lines were reverted. */
    public function revokeOrder(int $orderId, string $reason): int
    {
        return $this->db->transaction(function () use ($orderId, $reason) {
            $grants = $this->db->rows('SELECT * FROM {grants} WHERE order_id = ? AND reverted_at IS NULL', [$orderId]);
            foreach ($grants as $g) {
                foreach (array_filter(explode(',', $g['license_ids'])) as $id) {
                    $g['kind'] === 'renewal' ? $this->shorten($id, (int) $g['days'], $reason) : $this->cancel($id, true, $reason);
                }
                $this->db->execute('UPDATE {grants} SET reverted_at = ? WHERE item_id = ?', [$this->now(), (int) $g['item_id']]);
            }
            return count($grants);
        });
    }

    /** New licenses sold in this order (not refunded), for the customer's e-mail and order page. */
    public function orderLicenses(int $orderId): array
    {
        $ids = [];
        foreach ($this->db->rows("SELECT license_ids FROM {grants} WHERE order_id = ? AND kind = 'new' AND reverted_at IS NULL ORDER BY item_id", [$orderId]) as $g) {
            array_push($ids, ...array_filter(explode(',', $g['license_ids'])));
        }
        return array_map(fn (string $id) => $this->license($id), $ids);
    }

    public function keyFor(string $licenseId): string
    {
        $this->license($licenseId);
        return Keys::derive($this->keySecret, $licenseId);
    }

    public function license(string $licenseId): array
    {
        $row = $this->db->rows('SELECT * FROM {licenses} WHERE id = ?', [$licenseId])[0] ?? null;
        if ($row === null) {
            throw new Error(404, 'license.not_found', 'Lisans bulunamadı.');
        }
        return $row;
    }

    /** $userId/$email: a customer's own licenses (guest orders are matched by e-mail). */
    public function licenses(?string $search = null, ?int $userId = null, int $limit = 200, ?string $email = null): array
    {
        $where = [];
        $params = [];
        if ($userId !== null) {
            $where[] = '(user_id = ? OR (customer_email IS NOT NULL AND customer_email = ?))';
            $params[] = $userId;
            $params[] = $email ?? '';
        }
        if ($search !== null && trim($search) !== '') {
            $where[] = '(id = ? OR customer_name LIKE ? OR customer_email LIKE ?)';
            $like = '%' . trim($search) . '%';
            array_push($params, trim($search), $like, $like);
        }
        $sql = 'SELECT * FROM {licenses}' . ($where ? ' WHERE ' . implode(' AND ', $where) : '') . ' ORDER BY created_at DESC LIMIT ' . max(1, min($limit, 500));
        return $this->db->rows($sql, $params);
    }

    public function activations(string $licenseId): array
    {
        return $this->db->rows('SELECT * FROM {activations} WHERE license_id = ? ORDER BY created_at', [$licenseId]);
    }

    public function effectiveEnd(array $license): int
    {
        return in_array($license['status'], ['active', 'past_due'], true)
            ? (int) $license['paid_until'] + $this->settings->paymentToleranceDays * 86400
            : (int) $license['paid_until'];
    }

    public function audit(string $action, ?string $licenseId, string $detail): void
    {
        $this->db->execute('INSERT INTO {audit} (at, action, license_id, detail) VALUES (?, ?, ?, ?)', [$this->now(), $action, $licenseId, mb_substr($detail, 0, 1000)]);
    }

    public static function iso(int $unix): string
    {
        return gmdate('Y-m-d\TH:i:s\Z', $unix);
    }

    private function sign(array $license, string $activationId, string $deviceHash): string
    {
        $validUntil = $this->effectiveEnd($license);
        $refresh = min($this->now() + $this->settings->refreshDays * 86400, $validUntil);
        return $this->signer->sign([
            'v' => 1,
            'lid' => $license['id'],
            'prd' => self::PRODUCT,
            'ed' => $license['edition'],
            'feat' => array_values(array_filter(array_map('trim', explode(',', (string) $license['features'])))),
            'aid' => $activationId,
            'dev' => $deviceHash,
            'max' => (int) $license['max_devices'],
            'iat' => self::iso($this->now()),
            'exp' => self::iso($validUntil),
            'rfr' => self::iso($refresh),
            'grace' => $this->settings->offlineGraceDays,
        ]);
    }

    /** @return array{0:array,1:array} */
    private function findActivation(?string $activationId, ?string $deviceHash): array
    {
        if ($activationId === null || $activationId === '' || !$this->validDevice($deviceHash)) {
            throw new Error(400, 'request.activation', 'Etkinleştirme bilgisi eksik.');
        }
        $a = $this->db->rows('SELECT * FROM {activations} WHERE id = ?', [$activationId])[0] ?? null;
        if ($a === null || !hash_equals($a['device_hash'], $deviceHash)) {
            throw new Error(404, 'activation.not_found', 'Bu bilgisayarın etkinleştirmesi bulunamadı. Lisans anahtarıyla yeniden etkinleştirin.');
        }
        if ($a['deactivated_at'] !== null) {
            throw new Error(403, 'activation.removed', 'Lisans bu bilgisayardan kaldırılmış. Lisans anahtarıyla yeniden etkinleştirebilirsiniz.');
        }
        return [$this->license($a['license_id']), $a];
    }

    private function endedMessage(array $license): string
    {
        return 'Lisansın süresi ' . gmdate('d.m.Y', (int) $license['paid_until']) . ' tarihinde doldu. Verileriniz silinmez; yenileme sonrası devam edebilirsiniz.';
    }

    private function requireDevice(?string $deviceHash): void
    {
        if (!$this->validDevice($deviceHash)) {
            throw new Error(400, 'request.device', 'Cihaz bilgisi eksik.');
        }
    }

    private function validDevice(?string $deviceHash): bool
    {
        return is_string($deviceHash) && strlen($deviceHash) >= 16 && strlen($deviceHash) <= 128 && ctype_alnum($deviceHash);
    }
}

/**
 * Masaüstünün çağırdığı uç noktalar, WordPress'ten bağımsız: [HTTP durum kodu, JSON gövdesi].
 * Yollar: v1/activate, v1/refresh, v1/deactivate, v1/trial, v1/updates/permission.
 */
final class Api
{
    public function __construct(private readonly Issuer $issuer)
    {
    }

    /** @return array{0:int,1:array} */
    public function handle(string $route, array $body): array
    {
        try {
            $result = match ($route) {
                'v1/activate' => $this->issuer->activate(self::str($body, 'licenseKey'), self::str($body, 'deviceHash'), self::str($body, 'machineLabel')),
                'v1/refresh' => $this->issuer->refresh(self::str($body, 'activationId'), self::str($body, 'deviceHash')),
                'v1/deactivate' => $this->issuer->deactivate(self::str($body, 'activationId'), self::str($body, 'deviceHash')),
                'v1/trial' => $this->issuer->trial(self::str($body, 'deviceHash')),
                'v1/updates/permission' => $this->issuer->updatePermission(self::str($body, 'activationId'), self::str($body, 'deviceHash')),
                default => throw new Error(404, 'request.unknown', 'Bilinmeyen işlem.'),
            };
            return [200, $result];
        } catch (Error $e) {
            return [$e->status, ['code' => $e->errorCode, 'message' => $e->getMessage()]];
        }
    }

    /** Velopack dosyası: yalnızca düz dosya adı, klasörün içinde. */
    public static function safeReleaseFile(string $root, string $file): ?string
    {
        if ($root === '' || $file === '' || strlen($file) > 200 || $file[0] === '.' || preg_match('/^[A-Za-z0-9._-]+$/', $file) !== 1) {
            return null;
        }
        $base = realpath($root);
        $full = realpath($root . DIRECTORY_SEPARATOR . $file);
        return $base !== false && $full !== false && str_starts_with($full, $base . DIRECTORY_SEPARATOR) && is_file($full) ? $full : null;
    }

    private static function str(array $body, string $name): ?string
    {
        return isset($body[$name]) && is_string($body[$name]) ? $body[$name] : null;
    }
}
