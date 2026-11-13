<?php
/**
 * Ödeme sağlayıcıları — resmi belgelerden (okunma: 2026-11-13):
 *  - iyzico Checkout Form: docs.iyzico.com (cf-initialize, cf-retrieve, hmacsha256-auth, response-signature-validation)
 *    ve github.com/iyzico/iyzipay-php (IyziAuthV2Generator). Kimlik: "IYZWSv2 " + base64("apiKey:…&randomKey:…&signature:"
 *    + hex(HMAC-SHA256(secret, randomKey + yol + gövde))). Sonuç imzası: HMAC-SHA256(secret, paymentStatus:paymentId:
 *    currency:basketId:conversationId:paidPrice:price:token), fiyatlarda sondaki sıfırlar atılır.
 *  - PayTR iFrame API: dev.paytr.com/iframe-api (1. ve 2. adım). paytr_token = base64(HMAC-SHA256(merchant_key,
 *    merchant_id.user_ip.merchant_oid.email.payment_amount.user_basket.no_installment.max_installment.currency.test_mode
 *    + merchant_salt)); bildirim hash = base64(HMAC-SHA256(merchant_key, merchant_oid.merchant_salt.status.total_amount)),
 *    yanıt yalnızca "OK"; aynı bildirim tekrar gelebilir.
 * Lisans YALNIZCA sağlayıcının doğrulanmış onayıyla verilir; tarayıcı yönlendirmesi tek başına bir şey kanıtlamaz.
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 */

namespace Vernuko\License;

/** HTTP POST: fn(string $url, array $headers, string|array $body): array{0:int,1:string} */
interface Http
{
    public function post(string $url, array $headers, string|array $body): array;
}

final class Iyzico
{
    public const LIVE = 'https://api.iyzipay.com';
    /** Belgedeki imza örneklerindeki sunucu; ilk test ödemesinde doğrulanmalı. */
    public const SANDBOX = 'https://sandbox-api.iyzipay.com';
    private const INIT = '/payment/iyzipos/checkoutform/initialize/auth/ecom';
    private const DETAIL = '/payment/iyzipos/checkoutform/auth/ecom/detail';

    public function __construct(private readonly Http $http, private readonly string $apiKey, private readonly string $secretKey, private readonly string $baseUrl = self::LIVE)
    {
    }

    /** "4990.00" → "4990", 4990.5 → "4990.5" (iyzico's format for prices and their signatures). */
    public static function price(int|float|string $value): string
    {
        $s = is_int($value) ? (string) $value : number_format((float) $value, 8, '.', '');
        return str_contains($s, '.') ? rtrim(rtrim($s, '0'), '.') : $s;
    }

    public static function kurusToPrice(int $kurus): string
    {
        return self::price(number_format($kurus / 100, 2, '.', ''));
    }

    public function authorization(string $path, string $body, string $randomKey): string
    {
        $signature = bin2hex(hash_hmac('sha256', $randomKey . $path . $body, $this->secretKey, true));
        return 'IYZWSv2 ' . base64_encode('apiKey:' . $this->apiKey . '&randomKey:' . $randomKey . '&signature:' . $signature);
    }

    /**
     * Starts the hosted payment page. Returns the page URL to send the customer to and iyzico's token for this
     * payment (kept with the order, so the result can be asked again if the customer never comes back).
     * $identityNumber: the buyer's T.C. kimlik no if known (iyzico requires a value).
     * @return array{url:string,token:string}
     */
    public function initialize(array $order, Plan $plan, string $callbackUrl, string $identityNumber): array
    {
        [$first, $last] = self::splitName($order['customer_name']);
        $address = ['contactName' => $order['customer_name'], 'city' => $order['city'], 'country' => 'Turkey', 'address' => $order['address']];
        $price = self::kurusToPrice((int) $order['amount']);
        $body = json_encode([
            'locale' => 'tr',
            'conversationId' => $order['id'],
            'price' => $price,
            'paidPrice' => $price,
            'currency' => 'TRY',
            'basketId' => $order['id'],
            'paymentGroup' => 'PRODUCT',
            'callbackUrl' => $callbackUrl,
            'buyer' => [
                'id' => substr(hash('sha256', $order['email']), 0, 20), 'name' => $first, 'surname' => $last, 'identityNumber' => $identityNumber,
                'email' => $order['email'], 'gsmNumber' => $order['phone'], 'registrationAddress' => $order['address'], 'city' => $order['city'],
                'country' => 'Turkey', 'ip' => $order['ip'],
            ],
            'shippingAddress' => $address,
            'billingAddress' => $address,
            'basketItems' => [['id' => $plan->id, 'name' => $plan->name, 'category1' => 'Yazılım', 'itemType' => 'VIRTUAL', 'price' => $price]],
        ], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR);

        $r = $this->call(self::INIT, $body);
        if (($r['status'] ?? '') !== 'success' || !is_string($r['paymentPageUrl'] ?? null) || !is_string($r['token'] ?? null)) {
            throw new Error(502, 'pay.iyzico', 'iyzico ödeme sayfası açılamadı: ' . (is_string($r['errorMessage'] ?? null) ? $r['errorMessage'] : 'bilinmeyen yanıt'));
        }
        if (isset($r['signature']) && !hash_equals(hash_hmac('sha256', $r['conversationId'] . ':' . $r['token'], $this->secretKey), (string) $r['signature'])) {
            throw new Error(502, 'pay.iyzico_signature', 'iyzico yanıtının imzası doğrulanamadı.');
        }
        return ['url' => $r['paymentPageUrl'], 'token' => $r['token']];
    }

    /**
     * After the customer returns with the token (or later, from the stored token): asks iyzico for the result and
     * verifies its signature. review: the payment went through but iyzico's fraud check is still pending
     * (fraudStatus 0) — neither paid nor failed yet; asking again later settles it.
     * @return array{orderId:string,paid:bool,review:bool,paymentId:string,priceKurus:int,currency:string,reason:string}
     */
    public function retrieve(string $token): array
    {
        $r = $this->call(self::DETAIL, json_encode(['locale' => 'tr', 'token' => $token], JSON_THROW_ON_ERROR));
        if (($r['status'] ?? '') !== 'success') {
            throw new Error(502, 'pay.iyzico', 'iyzico ödeme sonucu alınamadı: ' . (is_string($r['errorMessage'] ?? null) ? $r['errorMessage'] : 'bilinmeyen yanıt'));
        }
        $fields = [(string) ($r['paymentStatus'] ?? ''), (string) ($r['paymentId'] ?? ''), (string) ($r['currency'] ?? ''), (string) ($r['basketId'] ?? ''),
            (string) ($r['conversationId'] ?? ''), self::price($r['paidPrice'] ?? '0'), self::price($r['price'] ?? '0'), (string) ($r['token'] ?? '')];
        if (!is_string($r['signature'] ?? null) || !hash_equals(hash_hmac('sha256', implode(':', $fields), $this->secretKey), $r['signature'])) {
            throw new Error(502, 'pay.iyzico_signature', 'iyzico sonucunun imzası doğrulanamadı; lisans oluşturulmadı.');
        }
        $success = strtoupper((string) $r['paymentStatus']) === 'SUCCESS';
        $fraud = (string) ($r['fraudStatus'] ?? '1');
        $paid = $success && $fraud === '1';
        return [
            'orderId' => (string) $r['basketId'],
            'paid' => $paid,
            'review' => $success && $fraud === '0',
            'paymentId' => (string) $r['paymentId'],
            'priceKurus' => (int) round((float) $r['price'] * 100),
            'currency' => (string) $r['currency'],
            'reason' => $paid ? '' : 'Ödeme durumu: ' . $r['paymentStatus'] . ', fraudStatus: ' . ($r['fraudStatus'] ?? '-'),
        ];
    }

    private function call(string $path, string $body): array
    {
        $random = bin2hex(random_bytes(8)) . time();
        [$status, $response] = $this->http->post($this->baseUrl . $path, [
            'Authorization' => $this->authorization($path, $body, $random),
            'Content-Type' => 'application/json',
            'Accept' => 'application/json',
        ], $body);
        $json = json_decode($response, true);
        if (!is_array($json)) {
            throw new Error(502, 'pay.iyzico', "iyzico'ya ulaşılamadı (HTTP $status).");
        }
        return $json;
    }

    /** @return array{0:string,1:string} */
    private static function splitName(string $name): array
    {
        $parts = preg_split('/\s+/', trim($name)) ?: [$name];
        $last = count($parts) > 1 ? array_pop($parts) : '-';
        return [implode(' ', $parts), $last];
    }
}

final class Paytr
{
    public const TOKEN_URL = 'https://www.paytr.com/odeme/api/get-token';

    public function __construct(private readonly Http $http, private readonly string $merchantId, private readonly string $merchantKey, private readonly string $merchantSalt, private readonly bool $testMode = false, private readonly string $tokenUrl = self::TOKEN_URL)
    {
    }

    public function token(string $hashStr): string
    {
        return base64_encode(hash_hmac('sha256', $hashStr . $this->merchantSalt, $this->merchantKey, true));
    }

    /** Step 1: returns the iframe token (the page shows https://www.paytr.com/odeme/guvenli/{token}). */
    public function iframeToken(array $order, Plan $plan, string $okUrl, string $failUrl): string
    {
        $basket = base64_encode(json_encode([[$plan->name, number_format($plan->priceKurus / 100, 2, '.', ''), 1]], JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR));
        $fields = [
            'merchant_id' => $this->merchantId,
            'user_ip' => $order['ip'],
            'merchant_oid' => $order['id'],
            'email' => $order['email'],
            'payment_amount' => (string) (int) $order['amount'],
            'user_basket' => $basket,
            'no_installment' => '0',
            'max_installment' => '0',
            'currency' => 'TL',
            'test_mode' => $this->testMode ? '1' : '0',
        ];
        $fields['paytr_token'] = $this->token(implode('', $fields));
        $fields += [
            'debug_on' => $this->testMode ? '1' : '0',
            'user_name' => mb_substr($order['customer_name'], 0, 60),
            'user_address' => mb_substr($order['address'] . ' ' . $order['city'], 0, 400),
            'user_phone' => (string) $order['phone'],
            'merchant_ok_url' => $okUrl,
            'merchant_fail_url' => $failUrl,
            'timeout_limit' => '30',
            'lang' => 'tr',
        ];
        [$status, $response] = $this->http->post($this->tokenUrl, [], $fields);
        $json = json_decode($response, true);
        if (!is_array($json) || ($json['status'] ?? '') !== 'success' || !is_string($json['token'] ?? null)) {
            throw new Error(502, 'pay.paytr', 'PayTR ödeme formu açılamadı: ' . (is_array($json) && is_string($json['reason'] ?? null) ? $json['reason'] : "HTTP $status"));
        }
        return $json['token'];
    }

    /**
     * Step 2: the notification PayTR posts to our "Bildirim URL". Verifies the hash; never trusts unsigned fields
     * for the amount (total_amount is signed; payment_amount is not).
     * @return array{orderId:string,paid:bool,totalKurus:int,currency:string,reason:string}
     */
    public function notification(array $post): array
    {
        foreach (['merchant_oid', 'status', 'total_amount', 'hash'] as $f) {
            if (!isset($post[$f]) || !is_string($post[$f])) {
                throw new Error(400, 'pay.paytr', 'PayTR bildirimi eksik.');
            }
        }
        $expected = base64_encode(hash_hmac('sha256', $post['merchant_oid'] . $this->merchantSalt . $post['status'] . $post['total_amount'], $this->merchantKey, true));
        if (!hash_equals($expected, $post['hash'])) {
            throw new Error(400, 'pay.paytr_hash', 'PAYTR notification failed: bad hash');
        }
        $currency = strtoupper((string) ($post['currency'] ?? 'TL'));
        return [
            'orderId' => $post['merchant_oid'],
            'paid' => $post['status'] === 'success',
            'totalKurus' => (int) $post['total_amount'],
            'currency' => $currency === 'TL' ? 'TRY' : $currency,
            'reason' => $post['status'] === 'success' ? '' : trim(($post['failed_reason_code'] ?? '') . ' ' . ($post['failed_reason_msg'] ?? '')),
        ];
    }
}
