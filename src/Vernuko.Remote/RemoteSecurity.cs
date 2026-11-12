using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Common;
using Vernuko.Persistence;

namespace Vernuko.Remote;

/// <summary>Pairing proofs: both sides show they know the one-time code AND see the same certificate.</summary>
public static class RemotePairing
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    /// <summary>Used when the test host runs without TLS on the loopback address.</summary>
    public const string PlainFingerprint = "plain-loopback";

    public static string NewCode()
    {
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }

    public static string Normalize(string code) => new string(code.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string Proof(string code, string role, string fingerprint, string nonce, string? deviceId = null) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)), Encoding.UTF8.GetBytes($"{role}|{fingerprint}|{nonce}|{deviceId}")));

    public static bool Equal(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public static string Fingerprint(X509Certificate2 certificate) => Convert.ToHexStringLower(SHA256.HashData(certificate.RawData));
}

/// <summary>The main computer's TLS certificate: self-signed, kept encrypted with the Windows account key.</summary>
public static class RemoteHostCertificate
{
    public static X509Certificate2 LoadOrCreate(IAppEnvironment env, ISecretProtector protector)
    {
        var path = Path.Combine(env.DataDirectory, "remote-host.pfx.protected");
        if (File.Exists(path))
        {
            return X509CertificateLoader.LoadPkcs12(protector.Unprotect(File.ReadAllBytes(path)), null, X509KeyStorageFlags.Exportable);
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Vernuko Ana Bilgisayar", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
        var pfx = created.Export(X509ContentType.Pfx);
        File.WriteAllBytes(path, protector.Protect(pfx));
        return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.Exportable);
    }
}

public sealed record PairedDevice(string Id, string Name, string KeyHash, DateTime PairedAtUtc, DateTime? LastSeenUtc);

/// <summary>Paired computers, stored in the main computer's database (only a hash of each device key).</summary>
public sealed class RemoteDeviceRegistry(IDbContextFactory<VernukoDbContext> factory, TimeProvider time)
{
    public const string SettingKey = "remote.devices";

    public async Task<IReadOnlyList<PairedDevice>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var json = await SettingsStore.GetAsync(db, SettingKey, ct);
        return json is null ? [] : JsonSerializer.Deserialize<List<PairedDevice>>(json, RpcJson.Options) ?? [];
    }

    public async Task<(string Id, string Key)> AddAsync(string name, CancellationToken ct = default)
    {
        var id = "pc_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = time.GetUtcNow().UtcDateTime;
        await UpdateAsync(list => list.Add(new PairedDevice(id, name.Length > 60 ? name[..60] : name, Hash(key), now, null)), ct);
        return (id, key);
    }

    public Task RemoveAsync(string id, CancellationToken ct = default) => UpdateAsync(list => list.RemoveAll(d => d.Id == id), ct);

    public async Task<bool> VerifyAsync(string? id, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
        {
            return false;
        }

        var device = (await ListAsync(ct)).FirstOrDefault(d => d.Id == id);
        return device is not null && RemotePairing.Equal(device.KeyHash, Hash(key));
    }

    public Task TouchAsync(string id, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return UpdateAsync(list =>
        {
            var i = list.FindIndex(d => d.Id == id);
            if (i >= 0 && (list[i].LastSeenUtc is null || now - list[i].LastSeenUtc > TimeSpan.FromMinutes(5)))
            {
                list[i] = list[i] with { LastSeenUtc = now };
            }
        }, ct);
    }

    private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private async Task UpdateAsync(Action<List<PairedDevice>> change, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var json = await SettingsStore.GetAsync(db, SettingKey, ct);
        var list = json is null ? [] : JsonSerializer.Deserialize<List<PairedDevice>>(json, RpcJson.Options) ?? [];
        var before = JsonSerializer.Serialize(list, RpcJson.Options);
        change(list);
        var after = JsonSerializer.Serialize(list, RpcJson.Options);
        if (after != before)
        {
            await SettingsStore.SetAsync(db, SettingKey, after, time.GetUtcNow().UtcDateTime, ct);
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}
