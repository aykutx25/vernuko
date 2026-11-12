using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application.Services;
using Vernuko.LicenseServer;
using Vernuko.Licensing;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.LicenseServer;

/// <summary>The real license server on a loopback port with a temp SQLite file.</summary>
public sealed class ServerRig : IAsyncDisposable
{
    public const string AdminKey = "test-admin-key-0123456789";
    public const string PaddleSecret = "pdl_ntfset_test_secret";
    public const string LaserPrice = "pri_standart_yearly";

    private readonly WebApplication _app;

    private ServerRig(WebApplication app, string root, ECDsa key, ManualTimeProvider time, HttpClient http, LicenseServerOptions options)
    {
        _app = app;
        Root = root;
        Key = key;
        Time = time;
        Http = http;
        Options = options;
    }

    public string Root { get; }

    public ECDsa Key { get; }

    public ManualTimeProvider Time { get; }

    public HttpClient Http { get; }

    public LicenseServerOptions Options { get; }

    public Uri BaseUri => Http.BaseAddress!;

    public IReadOnlyDictionary<string, byte[]> PublicKeys => new Dictionary<string, byte[]> { ["test-1"] = Key.ExportSubjectPublicKeyInfo() };

    public static async Task<ServerRig> StartAsync(ManualTimeProvider? time = null)
    {
        var root = Directory.CreateTempSubdirectory("vernuko-licsrv-").FullName;
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        time ??= new ManualTimeProvider(TestApp.DefaultNow);
        var options = new LicenseServerOptions
        {
            ConnectionString = $"Data Source={Path.Combine(root, "license.db")}",
            ReleasesPath = Directory.CreateDirectory(Path.Combine(root, "releases")).FullName,
            PaddlePrices = { [LaserPrice] = new PlanDefinition { Edition = "Standart", MaxDevices = 1 } },
        };
        var secrets = ServerSecrets.Create(key, "test-1", RandomNumberGenerator.GetBytes(32), AdminKey, PaddleSecret);
        var app = LicenseServerApp.Build([], b =>
        {
            b.Services.AddSingleton(options);
            b.Services.AddSingleton(secrets);
            b.Services.AddSingleton<TimeProvider>(time);
        });
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<LicenseServerDb>().Database.MigrateAsync();
        }

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new ServerRig(app, root, key, time, new HttpClient { BaseAddress = new Uri(address + "/") }, options);
    }

    public async Task<CreatedLicense> CreateLicenseAsync(int maxDevices = 1, DateTime? paidUntil = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/admin/licenses")
        {
            Content = JsonContent.Create(new CreateLicenseRequest("Işıl Güzellik", "isil@example.com", "Standart", maxDevices, paidUntil ?? TestApp.DefaultNow.UtcDateTime.AddYears(1), null, "test")),
        };
        request.Headers.Add(LicenseServerApp.AdminKeyHeader, AdminKey);
        using var response = await Http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedLicense>())!;
    }

    public async Task<HttpResponseMessage> PostAsync(string path, object body) => await Http.PostAsJsonAsync(path, body);

    public async Task<HttpResponseMessage> PaddleAsync(string json, string? secret = null, long? ts = null)
    {
        var timestamp = (ts ?? Time.GetUtcNow().ToUnixTimeSeconds()).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var h1 = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret ?? PaddleSecret), Encoding.UTF8.GetBytes(timestamp + ":" + json)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/webhooks/paddle") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("Paddle-Signature", $"ts={timestamp};h1={h1}");
        return await Http.SendAsync(request);
    }

    public async Task<T> DbAsync<T>(Func<LicenseServerDb, Task<T>> query)
    {
        using var scope = _app.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<LicenseServerDb>());
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        Key.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
        }
    }
}

public class LicenseKeyTests
{
    [Fact]
    public void Keys_are_derived_deterministically_and_normalized_forgivingly()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var key = LicenseKeys.Derive(secret, "lic_abc");
        Assert.Equal(key, LicenseKeys.Derive(secret, "lic_abc"));
        Assert.NotEqual(key, LicenseKeys.Derive(secret, "lic_abd"));
        Assert.Matches("^VRNK-[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{5}){3}$", key);

        var normalized = LicenseKeys.Normalize(key);
        Assert.NotNull(normalized);
        Assert.Equal(normalized, LicenseKeys.Normalize(" " + key.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) + " "));
        Assert.Equal(normalized, LicenseKeys.Normalize(key[5..])); // without the VRNK- prefix
        Assert.Null(LicenseKeys.Normalize("VRNK-123"));
        Assert.Null(LicenseKeys.Normalize(null));
    }

    [Fact]
    public void Paddle_signature_check_accepts_rotation_and_rejects_tampering_and_old_timestamps()
    {
        var secret = Encoding.UTF8.GetBytes("pdl_ntfset_x");
        var body = Encoding.UTF8.GetBytes("{\"event_id\":\"evt_1\"}");
        var now = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        var ts = new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        string Sign(byte[] s, byte[] b) => Convert.ToHexStringLower(HMACSHA256.HashData(s, Encoding.ASCII.GetBytes(ts + ":").Concat(b).ToArray()));
        var good = Sign(secret, body);
        var tolerance = TimeSpan.FromMinutes(5);

        Assert.True(PaddleWebhooks.VerifySignature($"ts={ts};h1={good}", body, secret, now, tolerance));
        Assert.True(PaddleWebhooks.VerifySignature($"ts={ts};h1={Sign(Encoding.UTF8.GetBytes("old"), body)};h1={good}", body, secret, now, tolerance));
        Assert.False(PaddleWebhooks.VerifySignature($"ts={ts};h1={good}", Encoding.UTF8.GetBytes("{\"event_id\":\"evt_2\"}"), secret, now, tolerance));
        Assert.False(PaddleWebhooks.VerifySignature($"ts={ts};h1={good}", body, Encoding.UTF8.GetBytes("other"), now, tolerance));
        Assert.False(PaddleWebhooks.VerifySignature($"ts={ts};h1={good}", body, secret, now.AddMinutes(10), tolerance));
        Assert.False(PaddleWebhooks.VerifySignature(null, body, secret, now, tolerance));
        Assert.False(PaddleWebhooks.VerifySignature("h1=" + good, body, secret, now, tolerance));
    }

    [Fact]
    public void Release_files_are_limited_to_plain_names_inside_the_folder()
    {
        var root = Directory.CreateTempSubdirectory("vernuko-rel-").FullName;
        File.WriteAllText(Path.Combine(root, "releases.win.json"), "{}");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(root)!, "secret.txt"), "x");
        Assert.NotNull(LicenseServerApp.SafeReleaseFile(root, "releases.win.json"));
        Assert.Null(LicenseServerApp.SafeReleaseFile(root, "../secret.txt"));
        Assert.Null(LicenseServerApp.SafeReleaseFile(root, "..%2Fsecret.txt"));
        Assert.Null(LicenseServerApp.SafeReleaseFile(root, ".hidden"));
        Assert.Null(LicenseServerApp.SafeReleaseFile(root, "missing.nupkg"));
        Assert.Null(LicenseServerApp.SafeReleaseFile(string.Empty, "releases.win.json"));
    }
}

public class LicenseServerTests
{
    private const string DeviceA = "0123456789abcdef0123456789abcdef";
    private const string DeviceB = "fedcba9876543210fedcba9876543210";

    private static async Task<(string License, string ActivationId)> ActivateAsync(ServerRig rig, string key, string device)
    {
        using var r = await rig.PostAsync("v1/activate", new { licenseKey = key, deviceHash = device });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var body = (await r.Content.ReadFromJsonAsync<LicenseResponse>())!;
        return (body.License, body.ActivationId);
    }

    private static async Task<ErrorResponse> ErrorAsync(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<ErrorResponse>())!;

    [Fact]
    public async Task Admin_endpoints_need_the_admin_key()
    {
        await using var rig = await ServerRig.StartAsync();
        using var r = await rig.PostAsync("v1/admin/licenses", new CreateLicenseRequest("X", null, null, 1, DateTime.UtcNow, null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        using var health = await rig.Http.GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Activation_issues_a_signed_license_for_one_device_and_moving_frees_the_seat()
    {
        await using var rig = await ServerRig.StartAsync();
        var created = await rig.CreateLicenseAsync(maxDevices: 1);
        var paidUntil = TestApp.DefaultNow.UtcDateTime.AddYears(1);

        var (signed, activationA) = await ActivateAsync(rig, created.Key.ToLowerInvariant(), DeviceA);
        var doc = new LicenseVerifier(rig.PublicKeys).Verify(signed);
        Assert.True(doc.IsSuccess);
        Assert.Equal(DeviceA, doc.Value.DeviceHash);
        Assert.Equal(created.Id, doc.Value.LicenseId);
        Assert.Equal(paidUntil.AddDays(7), doc.Value.ValidUntilUtc); // payment tolerance
        Assert.Equal(DateTimeKind.Utc, doc.Value.ValidUntilUtc.Kind); // read back from SQLite as UTC, serialized with Z
        Assert.Equal(TestApp.DefaultNow.UtcDateTime.AddDays(7), doc.Value.RefreshAfterUtc);

        // Same device again: same activation (no extra seat used).
        Assert.Equal(activationA, (await ActivateAsync(rig, created.Key, DeviceA)).ActivationId);

        using (var second = await rig.PostAsync("v1/activate", new { licenseKey = created.Key, deviceHash = DeviceB }))
        {
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal("license.seats", (await ErrorAsync(second)).Code);
        }

        using (var off = await rig.PostAsync("v1/deactivate", new { activationId = activationA, deviceHash = DeviceA }))
        {
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        }

        var (_, activationB) = await ActivateAsync(rig, created.Key, DeviceB);
        Assert.NotEqual(activationA, activationB);
        using var stale = await rig.PostAsync("v1/refresh", new { activationId = activationA, deviceHash = DeviceA });
        Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
        Assert.Equal("activation.removed", (await ErrorAsync(stale)).Code);

        // Another device cannot use someone else's activation id.
        using var stolen = await rig.PostAsync("v1/refresh", new { activationId = activationB, deviceHash = DeviceA });
        Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
    }

    [Fact]
    public async Task Wrong_or_malformed_keys_are_refused_with_clear_messages()
    {
        await using var rig = await ServerRig.StartAsync();
        await rig.CreateLicenseAsync();
        using var wrong = await rig.PostAsync("v1/activate", new { licenseKey = "VRNK-00000-00000-00000-00000", deviceHash = DeviceA });
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal("license.not_found", (await ErrorAsync(wrong)).Code);
        using var bad = await rig.PostAsync("v1/activate", new { licenseKey = "abc", deviceHash = DeviceA });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("VRNK-", (await ErrorAsync(bad)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ended_license_refuses_refresh_but_cancelling_keeps_the_paid_period()
    {
        await using var rig = await ServerRig.StartAsync();
        var paidUntil = TestApp.DefaultNow.UtcDateTime.AddDays(30);
        var created = await rig.CreateLicenseAsync(paidUntil: paidUntil);
        var (_, activation) = await ActivateAsync(rig, created.Key, DeviceA);

        using (var cancel = new HttpRequestMessage(HttpMethod.Post, $"v1/admin/licenses/{created.Id}/cancel"))
        {
            cancel.Headers.Add(LicenseServerApp.AdminKeyHeader, ServerRig.AdminKey);
            using var r = await rig.Http.SendAsync(cancel);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        rig.Time.Advance(TimeSpan.FromDays(10));
        using (var refresh = await rig.PostAsync("v1/refresh", new { activationId = activation, deviceHash = DeviceA }))
        {
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            var doc = new LicenseVerifier(rig.PublicKeys).Verify((await refresh.Content.ReadFromJsonAsync<LicenseResponse>())!.License).Value;
            Assert.Equal(paidUntil, doc.ValidUntilUtc); // cancelled: exactly the paid period, no tolerance
        }

        rig.Time.Advance(TimeSpan.FromDays(21));
        using var ended = await rig.PostAsync("v1/refresh", new { activationId = activation, deviceHash = DeviceA });
        Assert.Equal(HttpStatusCode.Forbidden, ended.StatusCode);
        Assert.Contains("silinmez", (await ErrorAsync(ended)).Message, StringComparison.Ordinal);
    }

    private static string SubscriptionEvent(string eventId, string status, DateTime occurred, DateTime? periodEnd, string subscriptionId = "sub_01h", string price = ServerRig.LaserPrice)
    {
        static string Iso(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = subscriptionId,
            ["status"] = status,
            ["customer_id"] = "ctm_01h",
            ["custom_data"] = null,
            ["current_billing_period"] = periodEnd is { } e ? new System.Text.Json.Nodes.JsonObject { ["starts_at"] = Iso(occurred), ["ends_at"] = Iso(e) } : null,
            ["items"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
            {
                ["status"] = "active",
                ["quantity"] = 1,
                ["price"] = new System.Text.Json.Nodes.JsonObject { ["id"] = price, ["product_id"] = "pro_1" },
            }),
            ["canceled_at"] = status == "canceled" ? Iso(occurred) : null,
            ["scheduled_change"] = null,
        };
        return new System.Text.Json.Nodes.JsonObject
        {
            ["event_id"] = eventId,
            ["event_type"] = "subscription." + (status == "active" ? "updated" : status),
            ["occurred_at"] = Iso(occurred),
            ["notification_id"] = "ntf_1",
            ["data"] = data,
        }.ToJsonString();
    }

    [Fact]
    public async Task Paddle_webhooks_create_and_extend_licenses_idempotently_and_ignore_stale_events()
    {
        await using var rig = await ServerRig.StartAsync();
        var now = TestApp.DefaultNow.UtcDateTime;

        using (var bad = await rig.PaddleAsync(SubscriptionEvent("evt_0", "active", now, now.AddMonths(1)), secret: "wrong"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var created = SubscriptionEvent("evt_1", "active", now, now.AddMonths(1));
        using (var r = await rig.PaddleAsync(created))
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        using (var again = await rig.PaddleAsync(created))
        {
            Assert.Contains("duplicate", await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        var license = await rig.DbAsync(db => db.Licenses.SingleAsync());
        Assert.Equal(ServerLicenseStatus.Active, license.Status);
        Assert.Equal(now.AddMonths(1), license.PaidUntilUtc);

        // Renewal for the next month, then a late (older) past_due event that must not win.
        using (await rig.PaddleAsync(SubscriptionEvent("evt_2", "active", now.AddMonths(1), now.AddMonths(2))))
        {
        }

        using (var late = await rig.PaddleAsync(SubscriptionEvent("evt_3", "past_due", now.AddDays(20), now.AddMonths(1))))
        {
            Assert.Contains("stale", await late.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        license = await rig.DbAsync(db => db.Licenses.SingleAsync());
        Assert.Equal(ServerLicenseStatus.Active, license.Status);
        Assert.Equal(now.AddMonths(2), license.PaidUntilUtc);
        Assert.Equal(3, await rig.DbAsync(db => db.WebhookEvents.CountAsync()));

        // Unknown price: recorded, no license.
        using (await rig.PaddleAsync(SubscriptionEvent("evt_4", "active", now, now.AddMonths(1), "sub_other", "pri_unknown"))) { }
        Assert.Equal(1, await rig.DbAsync(db => db.Licenses.CountAsync()));

        // Cancellation ends at the cancel time.
        using (await rig.PaddleAsync(SubscriptionEvent("evt_5", "canceled", now.AddMonths(2), null))) { }
        license = await rig.DbAsync(db => db.Licenses.SingleAsync());
        Assert.Equal(ServerLicenseStatus.Cancelled, license.Status);
        Assert.Equal(now.AddMonths(2), license.PaidUntilUtc);
    }

    [Fact]
    public async Task Update_feed_is_served_only_to_an_activated_installation()
    {
        await using var rig = await ServerRig.StartAsync();
        File.WriteAllText(Path.Combine(rig.Options.ReleasesPath, "releases.win.json"), "{\"Assets\":[]}");
        var created = await rig.CreateLicenseAsync();
        var (_, activation) = await ActivateAsync(rig, created.Key, DeviceA);

        using (var anonymous = await rig.Http.GetAsync("v1/updates/feed/releases.win.json"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/updates/feed/releases.win.json");
        request.Headers.Add(LicenseServerApp.ActivationHeader, activation);
        request.Headers.Add(LicenseServerApp.DeviceHeader, DeviceA);
        using var ok = await rig.Http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("{\"Assets\":[]}", await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Desktop_activates_refreshes_and_moves_the_license_through_the_real_server()
    {
        await using var rig = await ServerRig.StartAsync();
        var created = await rig.CreateLicenseAsync();
        var licensing = new LicensingOptions { PublicKeys = rig.PublicKeys, ServerUrl = rig.BaseUri };
        await using var app = await TestApp.CreateAsync(time: rig.Time, device: new TestDevice(DeviceA), licensing: licensing);
        await app.SetupBeautyAsync();
        var online = app.Get<LicenseOnlineService>();
        Assert.True(online.IsAvailable);

        var activated = await online.ActivateAsync(created.Key);
        Assert.True(activated.IsSuccess, activated.IsFailure ? activated.Error.ToString() : null);
        Assert.Equal(LicenseState.Active, activated.Value.State);
        var first = (await app.Get<LicenseService>().GetDocumentAsync())!;

        // A week later the background check-in fetches a fresh document.
        rig.Time.Advance(TimeSpan.FromDays(8));
        await online.AutoRefreshAsync();
        var second = (await app.Get<LicenseService>().GetDocumentAsync())!;
        Assert.True(second.IssuedAtUtc > first.IssuedAtUtc);
        Assert.Equal(LicenseState.Active, app.Get<LicenseService>().Current.State);

        // Moving to a new computer: seat freed, local license removed, data untouched.
        var customer = await app.AddCustomerAsync("Taşıma", "Testi", "0532 111 22 33");
        var moved = await online.DeactivateAsync();
        Assert.True(moved.IsSuccess, moved.IsFailure ? moved.Error.ToString() : null);
        Assert.Null(await app.Get<LicenseService>().GetDocumentAsync());
        Assert.True((await app.Get<CustomerService>().GetAsync(customer)).IsSuccess);

        // The new computer can now activate with the same key.
        using var newPc = await rig.PostAsync("v1/activate", new { licenseKey = created.Key, deviceHash = DeviceB });
        Assert.Equal(HttpStatusCode.OK, newPc.StatusCode);
    }

    [Fact]
    public async Task Desktop_reports_offline_without_locking_and_adopts_the_earlier_server_trial()
    {
        var time = new ManualTimeProvider(TestApp.DefaultNow);
        await using var rig = await ServerRig.StartAsync(time);
        using (await rig.PostAsync("v1/trial", new { deviceHash = DeviceA }))
        {
        }

        // Reinstall 20 days later: local trial would restart, the server remembers the first start.
        time.Advance(TimeSpan.FromDays(20));
        await using var app = await TestApp.CreateAsync(time: time, device: new TestDevice(DeviceA), licensing: new LicensingOptions { PublicKeys = rig.PublicKeys, ServerUrl = rig.BaseUri });
        await app.SetupBeautyAsync();
        Assert.Equal(30, app.Get<LicenseService>().Current.DaysLeft);
        var synced = await app.Get<LicenseOnlineService>().SyncTrialAsync();
        Assert.True(synced.IsSuccess, synced.IsFailure ? synced.Error.ToString() : null);
        Assert.Equal(10, synced.Value.DaysLeft);

        // Unreachable server: a clear message, nothing changes locally.
        await using var offline = await TestApp.CreateAsync(device: new TestDevice(DeviceB), licensing: new LicensingOptions { PublicKeys = rig.PublicKeys, ServerUrl = new Uri("http://127.0.0.1:9/") });
        await offline.SetupBeautyAsync();
        var r = await offline.Get<LicenseOnlineService>().ActivateAsync("VRNK-00000-00000-00000-00000");
        Assert.True(r.IsFailure);
        Assert.Equal("license.offline", r.Error.Code);
        Assert.True(offline.Get<LicenseService>().Current.CanWrite);
    }
}
