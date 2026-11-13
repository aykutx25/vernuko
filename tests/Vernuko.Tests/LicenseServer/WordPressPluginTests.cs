using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using Vernuko.Application.Services;
using Vernuko.Licensing;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.LicenseServer;

/// <summary>
/// The WordPress license plugin's core (wordpress/vernuko-license) served by PHP's built-in server, and the real
/// desktop client talking to it. Proves the PHP side speaks exactly the desktop's protocol and signature format.
/// Skipped where PHP (with openssl and pdo_sqlite) is not installed.
/// </summary>
public sealed class PhpPluginServer : IAsyncDisposable
{
    private readonly Process _process;

    private PhpPluginServer(Process process, Uri baseUri, string root, string publicKey, string clockFile)
    {
        _process = process;
        BaseUri = baseUri;
        Root = root;
        PublicKey = Convert.FromBase64String(publicKey);
        ClockFile = clockFile;
    }

    public Uri BaseUri { get; }

    public string Root { get; }

    public byte[] PublicKey { get; }

    public string ClockFile { get; }

    /// <summary>PHP with the extensions the plugin needs (openssl, pdo_sqlite), or null (tests are then skipped).</summary>
    public static string? Php { get; } = FindUsablePhp();

    private static string? FindUsablePhp()
    {
        var php = FindPhp();
        if (php is null)
        {
            return null;
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo(php, "-m") { RedirectStandardOutput = true, UseShellExecute = false })!;
            var modules = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10_000);
            return modules.Contains("openssl", StringComparison.OrdinalIgnoreCase) && modules.Contains("pdo_sqlite", StringComparison.OrdinalIgnoreCase) ? php : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    public static async Task<PhpPluginServer> StartAsync(DateTimeOffset now)
    {
        var plugin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wordpress", "vernuko-license"));
        var root = Directory.CreateTempSubdirectory("vernuko-php-").FullName;
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyFile = Path.Combine(root, "key.pem");
        File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
        var clock = Path.Combine(root, "clock.txt");
        File.WriteAllText(clock, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path.Combine(root, "releases"));

        var port = FreePort();
        var psi = new ProcessStartInfo(Php!, $"-S 127.0.0.1:{port} \"{Path.Combine(plugin, "tests", "dev-server.php")}\"")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        psi.Environment["VK_DB"] = Path.Combine(root, "license.db");
        psi.Environment["VK_KEY"] = keyFile;
        psi.Environment["VK_SECRET"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        psi.Environment["VK_CLOCK"] = clock;
        psi.Environment["VK_RELEASES"] = Path.Combine(root, "releases");
        var process = Process.Start(psi)!;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var server = new PhpPluginServer(process, new Uri($"http://127.0.0.1:{port}/wp-json/vernuko/"), root, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), clock);
        using var http = new HttpClient();
        for (var i = 0; i < 50; i++)
        {
            try
            {
                using var r = await http.PostAsJsonAsync(new Uri(server.BaseUri, "v1/trial"), new { deviceHash = "warmupwarmupwarmup00" });
                return server;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException("PHP server did not start.");
    }

    public void SetNow(DateTimeOffset now) => File.WriteAllText(ClockFile, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    public async Task<(string Id, string Key)> CreateLicenseAsync(int devices = 1, int days = 365)
    {
        using var http = new HttpClient();
        using var r = await http.PostAsJsonAsync(new Uri(BaseUri, "test/create"), new { name = "Işıl Güzellik", devices, days });
        var body = (await r.Content.ReadFromJsonAsync<Dictionary<string, string>>())!;
        return (body["id"], body["key"]);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }

    private static string? FindPhp()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            foreach (var name in new[] { "php", "php.exe" })
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}

public class WordPressPluginTests
{
    private const string DeviceA = "0123456789abcdef0123456789abcdef";
    private const string DeviceB = "fedcba9876543210fedcba9876543210";

    [Fact]
    public async Task Php_core_tests_pass()
    {
        Assert.SkipWhen(PhpPluginServer.Php is null, "PHP (openssl + pdo_sqlite) yüklü değil");
        var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wordpress", "vernuko-license", "tests", "core-test.php"));
        var psi = new ProcessStartInfo(PhpPluginServer.Php!, $"\"{script}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync() + await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        Assert.True(p.ExitCode == 0, output);
    }

    [Fact]
    public async Task Desktop_activates_refreshes_and_moves_the_license_against_the_wordpress_plugin()
    {
        Assert.SkipWhen(PhpPluginServer.Php is null, "PHP (openssl + pdo_sqlite) yüklü değil");
        var time = new ManualTimeProvider(TestApp.DefaultNow);
        await using var php = await PhpPluginServer.StartAsync(time.GetUtcNow());
        var license = await php.CreateLicenseAsync();

        var options = new LicensingOptions { PublicKeys = new Dictionary<string, byte[]> { ["test-php"] = php.PublicKey }, ServerUrl = php.BaseUri };
        await using var app = await TestApp.CreateAsync(time: time, device: new TestDevice(DeviceA), licensing: options);
        await app.SetupBeautyAsync();
        var online = app.Get<LicenseOnlineService>();

        // The signed document from PHP verifies with the desktop's verifier (ECDSA P-256, r||s, base64url).
        var activated = await online.ActivateAsync(license.Key.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        Assert.True(activated.IsSuccess, activated.IsFailure ? activated.Error.ToString() : null);
        Assert.Equal(LicenseState.Active, activated.Value.State);
        var doc = (await app.Get<LicenseService>().GetDocumentAsync())!;
        Assert.Equal(license.Id, doc.LicenseId);
        Assert.Equal(TestApp.DefaultNow.UtcDateTime.AddDays(372), doc.ValidUntilUtc);
        Assert.Equal(DeviceA, doc.DeviceHash);

        // A week later the background check-in gets a fresh document.
        time.Advance(TimeSpan.FromDays(8));
        php.SetNow(time.GetUtcNow());
        await online.AutoRefreshAsync();
        Assert.True((await app.Get<LicenseService>().GetDocumentAsync())!.IssuedAtUtc > doc.IssuedAtUtc);

        // A second computer is refused with the plugin's Turkish message, then allowed after moving.
        using var http = new HttpClient();
        using (var second = await http.PostAsJsonAsync(new Uri(php.BaseUri, "v1/activate"), new { licenseKey = license.Key, deviceHash = DeviceB }))
        {
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Contains("Bu bilgisayardan kaldır", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        Assert.True((await online.DeactivateAsync()).IsSuccess);
        Assert.Null(await app.Get<LicenseService>().GetDocumentAsync());
        using var moved = await http.PostAsJsonAsync(new Uri(php.BaseUri, "v1/activate"), new { licenseKey = license.Key, deviceHash = DeviceB });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        // Errors come through as the plugin's messages.
        var wrong = await online.ActivateAsync("VRNK-00000-00000-00000-00000");
        Assert.Equal("license.not_found", wrong.Error.Code);
    }

    [Fact]
    public async Task Desktop_adopts_the_earlier_trial_start_from_the_wordpress_plugin()
    {
        Assert.SkipWhen(PhpPluginServer.Php is null, "PHP (openssl + pdo_sqlite) yüklü değil");
        var time = new ManualTimeProvider(TestApp.DefaultNow);
        await using var php = await PhpPluginServer.StartAsync(time.GetUtcNow());
        using (var http = new HttpClient())
        using (await http.PostAsJsonAsync(new Uri(php.BaseUri, "v1/trial"), new { deviceHash = DeviceA }))
        {
        }

        time.Advance(TimeSpan.FromDays(20));
        php.SetNow(time.GetUtcNow());
        await using var app = await TestApp.CreateAsync(time: time, device: new TestDevice(DeviceA),
            licensing: new LicensingOptions { PublicKeys = new Dictionary<string, byte[]> { ["test-php"] = php.PublicKey }, ServerUrl = php.BaseUri });
        await app.SetupBeautyAsync();
        var synced = await app.Get<LicenseOnlineService>().SyncTrialAsync();
        Assert.True(synced.IsSuccess, synced.IsFailure ? synced.Error.ToString() : null);
        Assert.Equal(10, synced.Value.DaysLeft);
    }

    /// <summary>
    /// Against a real WordPress + WooCommerce site with the plugin (opt-in: VERNUKO_WP_URL, VERNUKO_WP_KEY,
    /// VERNUKO_WP_KEY_ID, VERNUKO_WP_PUBLIC_KEY). See wordpress/vernuko-license/README.md.
    /// </summary>
    [Fact]
    public async Task Desktop_works_against_a_real_wordpress_site()
    {
        var url = Environment.GetEnvironmentVariable("VERNUKO_WP_URL");
        Assert.SkipWhen(string.IsNullOrEmpty(url), "Gerçek WordPress sitesi verilmedi (VERNUKO_WP_URL)");
        var options = new LicensingOptions
        {
            PublicKeys = new Dictionary<string, byte[]> { [Environment.GetEnvironmentVariable("VERNUKO_WP_KEY_ID")!] = Convert.FromBase64String(Environment.GetEnvironmentVariable("VERNUKO_WP_PUBLIC_KEY")!) },
            ServerUrl = new Uri(url!),
        };
        var device = new TestDevice(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
        await using var app = await TestApp.CreateAsync(device: device, licensing: options);
        await app.SetupBeautyAsync();
        var online = app.Get<LicenseOnlineService>();

        var activated = await online.ActivateAsync(Environment.GetEnvironmentVariable("VERNUKO_WP_KEY")!);
        Assert.True(activated.IsSuccess, activated.IsFailure ? activated.Error.ToString() : null);
        Assert.Equal(LicenseState.Active, activated.Value.State);
        Assert.True((await online.RefreshAsync()).IsSuccess);
        Assert.True((await online.DeactivateAsync()).IsSuccess);
        Assert.Null(await app.Get<LicenseService>().GetDocumentAsync());
    }
}
