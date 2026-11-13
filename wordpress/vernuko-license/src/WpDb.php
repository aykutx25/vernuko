<?php
/**
 * Çekirdeğin veritabanı bağlantısı WordPress'te: $wpdb. "?" yer tutucuları $wpdb->prepare biçimine çevrilir;
 * null değerler gerçek NULL olarak yazılır ($wpdb->prepare null'u boş metne çevirdiği için).
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

namespace Vernuko\License;

final class WpDb implements Db
{
    public function __construct(private readonly \wpdb $wpdb)
    {
    }

    public function rows(string $sql, array $params = []): array
    {
        $result = $this->wpdb->get_results($this->prepare($sql, $params), ARRAY_A);
        // get_results() returns [] on an SQL error, which read as "no rows" (e.g. "nothing to revoke"); every query
        // resets last_error, so a non-empty one belongs to this query.
        if ($this->wpdb->last_error !== '') {
            throw new \RuntimeException('Veritabanı hatası: ' . $this->wpdb->last_error);
        }
        return $result ?? [];
    }

    public function execute(string $sql, array $params = []): int
    {
        $result = $this->wpdb->query($this->prepare($sql, $params));
        if ($result === false) {
            throw new \RuntimeException('Veritabanı hatası: ' . $this->wpdb->last_error);
        }
        return (int) $result;
    }

    /**
     * Shared by every WpDb object: they all use the one $wpdb connection, and MySQL commits the open transaction
     * when a second START TRANSACTION arrives (Orders and Issuer each have their own WpDb).
     */
    private static int $depth = 0;

    /** Re-entrant: only the outermost call begins and commits; any failure rolls back everything. */
    public function transaction(callable $work): mixed
    {
        if (self::$depth > 0) {
            self::$depth++;
            try {
                return $work();
            } finally {
                self::$depth--;
            }
        }
        $this->wpdb->query('START TRANSACTION');
        self::$depth = 1;
        try {
            $result = $work();
            $this->wpdb->query('COMMIT');
            return $result;
        } catch (\Throwable $e) {
            $this->wpdb->query('ROLLBACK');
            throw $e;
        } finally {
            self::$depth = 0;
        }
    }

    private function prepare(string $sql, array $params): string
    {
        $sql = preg_replace('/\{(\w+)\}/', $this->wpdb->prefix . 'vernuko_$1', $sql);
        $parts = explode('?', $sql);
        if (count($parts) - 1 !== count($params)) {
            throw new \InvalidArgumentException('Parametre sayısı uyuşmuyor.');
        }
        $out = $parts[0];
        foreach (array_values($params) as $i => $value) {
            if ($value === null) {
                $out .= 'NULL';
            } elseif (is_int($value)) {
                $out .= (string) $value;
            } else {
                $out .= $this->wpdb->prepare('%s', (string) $value);
            }
            $out .= $parts[$i + 1];
        }
        return $out;
    }
}
