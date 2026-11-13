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
        if ($result === null && $this->wpdb->last_error !== '') {
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

    public function transaction(callable $work): mixed
    {
        $this->wpdb->query('START TRANSACTION');
        try {
            $result = $work();
            $this->wpdb->query('COMMIT');
            return $result;
        } catch (\Throwable $e) {
            $this->wpdb->query('ROLLBACK');
            throw $e;
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
