using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Vernuko.LicenseServer;

/// <summary>
/// Paddle Billing webhooks (https://developer.paddle.com/webhooks/signature-verification and
/// …/webhooks/subscriptions/subscription-updated, read 2026-11-12):
/// header <c>Paddle-Signature: ts=…;h1=…</c>, h1 = lowercase hex HMAC-SHA256(secret, ts + ":" + raw body);
/// several h1 values may be present during secret rotation. Paddle retries (same event_id may arrive again) and
/// does not guarantee order (compare occurred_at). A license is only ever created/extended from a verified
/// webhook, never from a browser redirect.
/// </summary>
public sealed class PaddleWebhooks(LicenseServerDb db, ServerSecrets secrets, LicenseServerOptions options, LicenseIssuer issuer, TimeProvider time)
{
    public const string ProviderName = "paddle";

    public static bool VerifySignature(string? header, byte[] rawBody, byte[] secret, DateTime nowUtc, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        string? ts = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            var name = part[..eq];
            var value = part[(eq + 1)..];
            if (name == "ts")
            {
                ts = value;
            }
            else if (name == "h1")
            {
                signatures.Add(value);
            }
        }

        if (ts is null || signatures.Count == 0 || !long.TryParse(ts, NumberStyles.None, CultureInfo.InvariantCulture, out var unix))
        {
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        if ((nowUtc - signedAt).Duration() > tolerance)
        {
            return false;
        }

        var payload = new byte[ts.Length + 1 + rawBody.Length];
        Encoding.ASCII.GetBytes(ts, payload);
        payload[ts.Length] = (byte)':';
        rawBody.CopyTo(payload, ts.Length + 1);
        var expected = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(HMACSHA256.HashData(secret, payload)));
        return signatures.Any(s => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(s.ToLowerInvariant()), expected));
    }

    /// <summary>Returns the HTTP status to answer with. 200 also for duplicates and ignored events (no retries).</summary>
    public async Task<(int Status, string Outcome)> HandleAsync(string? signatureHeader, byte[] rawBody, CancellationToken ct)
    {
        if (secrets.PaddleSecret is null)
        {
            return (404, "paddle disabled");
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (!VerifySignature(signatureHeader, rawBody, secrets.PaddleSecret, now, TimeSpan.FromSeconds(options.PaddleTimestampToleranceSeconds)))
        {
            return (401, "bad signature");
        }

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return (400, "bad json");
        }

        using (json)
        {
            var root = json.RootElement;
            var eventId = Str(root, "event_id");
            var eventType = Str(root, "event_type");
            var occurredAt = Date(root, "occurred_at");
            if (eventId is null || eventType is null || occurredAt is null || !root.TryGetProperty("data", out var data))
            {
                return (400, "missing fields");
            }

            if (await db.WebhookEvents.AnyAsync(e => e.Provider == ProviderName && e.EventId == eventId, ct))
            {
                return (200, "duplicate");
            }

            var (outcome, licenseId) = eventType.StartsWith("subscription.", StringComparison.Ordinal)
                ? await ApplySubscriptionAsync(data, occurredAt.Value, ct)
                : ("ignored", (string?)null);
            db.WebhookEvents.Add(new WebhookEvent
            {
                Provider = ProviderName,
                EventId = eventId,
                EventType = eventType,
                OccurredAtUtc = occurredAt.Value,
                ReceivedAtUtc = now,
                Outcome = outcome,
                LicenseId = licenseId,
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two deliveries of the same event at once: the unique key let only one through.
                if (await IsDuplicateAsync(eventId, ct))
                {
                    return (200, "duplicate");
                }

                throw;
            }

            return (200, outcome);
        }
    }

    private async Task<bool> IsDuplicateAsync(string eventId, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return await db.WebhookEvents.AnyAsync(e => e.Provider == ProviderName && e.EventId == eventId, ct);
    }

    private async Task<(string Outcome, string? LicenseId)> ApplySubscriptionAsync(JsonElement data, DateTime occurredAt, CancellationToken ct)
    {
        var subscriptionId = Str(data, "id");
        var status = Str(data, "status");
        if (subscriptionId is null || status is null)
        {
            return ("missing subscription id/status", null);
        }

        var license = await db.Licenses.FirstOrDefaultAsync(l => l.Provider == ProviderName && l.ProviderSubscriptionId == subscriptionId, ct);
        if (license is not null && license.ProviderEventAtUtc is { } last && last > occurredAt)
        {
            return ("stale (older than the last applied event)", license.Id);
        }

        DateTime? periodEnd = data.TryGetProperty("current_billing_period", out var period) && period.ValueKind == JsonValueKind.Object ? Date(period, "ends_at") : null;
        if (license is null)
        {
            if (status is not ("active" or "trialing" or "past_due") || periodEnd is null)
            {
                return ($"no license for {status} subscription", null);
            }

            var priceId = data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(i => i.TryGetProperty("price", out var p) ? Str(p, "id") : null).FirstOrDefault(id => id is not null && options.PaddlePrices.ContainsKey(id))
                : null;
            if (priceId is null)
            {
                return ("unknown price", null);
            }

            var plan = options.PaddlePrices[priceId];
            license = new ServerLicense
            {
                Id = LicenseKeys.NewLicenseId(),
                Edition = plan.Edition,
                MaxDevices = plan.MaxDevices,
                Features = string.Join(',', plan.Features),
                Provider = ProviderName,
                ProviderSubscriptionId = subscriptionId,
                ProviderCustomerId = Str(data, "customer_id"),
                CustomerName = Str(data, "customer_id") ?? "Paddle müşterisi",
                CreatedAtUtc = time.GetUtcNow().UtcDateTime,
                PaidUntilUtc = periodEnd.Value,
            };
            license.KeyHash = LicenseKeys.Hash(LicenseKeys.Normalize(LicenseKeys.Derive(secrets.KeySecret, license.Id))!);
            db.Licenses.Add(license);
            issuer.Audit("license.created", license.Id, $"Paddle {subscriptionId} ({plan.Edition})");
        }

        license.ProviderEventAtUtc = occurredAt;
        switch (status)
        {
            case "active" or "trialing":
                license.Status = ServerLicenseStatus.Active;
                if (periodEnd is { } end && end > license.PaidUntilUtc)
                {
                    license.PaidUntilUtc = end;
                }

                break;
            case "past_due":
                license.Status = ServerLicenseStatus.PastDue; // payment tolerance + offline grace keep it working
                break;
            case "paused":
                license.Status = ServerLicenseStatus.Suspended;
                license.PaidUntilUtc = Min(license.PaidUntilUtc, Date(data, "paused_at") ?? occurredAt);
                break;
            case "canceled":
                license.Status = ServerLicenseStatus.Cancelled;
                license.PaidUntilUtc = Min(license.PaidUntilUtc, Date(data, "canceled_at") ?? occurredAt);
                break;
            default:
                return ($"unknown status {status}", license.Id);
        }

        issuer.Audit("license.provider_update", license.Id, $"{status}, ödenen dönem sonu {license.PaidUntilUtc:yyyy-MM-dd}");
        return ($"applied {status}", license.Id);
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? Date(JsonElement e, string name) =>
        Str(e, name) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d.UtcDateTime : null;
}
