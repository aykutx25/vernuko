using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vernuko.Core;

namespace Vernuko.Licensing;

/// <summary>
/// Verifies license signatures with ECDSA P-256 / SHA-256 (built into .NET on all supported Windows versions,
/// no third-party crypto dependency). Multiple public keys allow key rotation via the key id.
/// </summary>
public sealed class LicenseVerifier
{
    private readonly IReadOnlyDictionary<string, byte[]> _publicKeys;
    private readonly string _product;

    /// <param name="publicKeys">Key id → SubjectPublicKeyInfo (DER) bytes.</param>
    /// <param name="product">The program this app is: a license signed for another Vernuko program is refused.</param>
    public LicenseVerifier(IReadOnlyDictionary<string, byte[]> publicKeys, string product = LicenseProducts.Randevu)
    {
        _publicKeys = publicKeys;
        _product = product;
    }

    public Result<LicenseDocument> Verify(string signedLicenseJson)
    {
        SignedLicense? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SignedLicense>(signedLicenseJson);
        }
        catch (JsonException)
        {
            return LicenseErrors.Malformed;
        }

        // A damaged file may lack any field: that is "malformed", never an exception.
        if (envelope is null || envelope.Algorithm != "ES256" || string.IsNullOrEmpty(envelope.KeyId) || envelope.Payload is null || envelope.Signature is null)
        {
            return LicenseErrors.Malformed;
        }

        if (!_publicKeys.TryGetValue(envelope.KeyId, out var spki))
        {
            return LicenseErrors.UnknownKey;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = Base64Url.Decode(envelope.Payload);
            signature = Base64Url.Decode(envelope.Signature);
        }
        catch (FormatException)
        {
            return LicenseErrors.Malformed;
        }

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
        if (!ecdsa.VerifyData(Encoding.ASCII.GetBytes(envelope.Payload), signature, HashAlgorithmName.SHA256))
        {
            return LicenseErrors.BadSignature;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<LicenseDocument>(payloadBytes);
            if (doc is null || doc.FormatVersion != 1)
            {
                return LicenseErrors.Malformed;
            }

            // Signed and valid, but for another program (a Vernuko Stok key typed into Vernuko Randevu).
            return string.Equals(doc.Product, _product, StringComparison.Ordinal) ? doc : LicenseErrors.WrongProduct(doc.Product, _product);
        }
        catch (JsonException)
        {
            return LicenseErrors.Malformed;
        }
    }

    /// <summary>Server/test-side helper. The desktop app never has access to a private key.</summary>
    public static string Sign(LicenseDocument document, ECDsa privateKey, string keyId)
    {
        var payload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(document));
        var signature = privateKey.SignData(Encoding.ASCII.GetBytes(payload), HashAlgorithmName.SHA256);
        return JsonSerializer.Serialize(new SignedLicense(keyId, payload, Base64Url.Encode(signature)));
    }
}

public static class Base64Url
{
    public static string Encode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid base64url length."),
        };
        return Convert.FromBase64String(s);
    }
}

public static class LicenseErrors
{
    public static readonly Error Malformed = new("license.malformed", "Lisans dosyası okunamadı.");
    public static readonly Error UnknownKey = new("license.unknown_key", "Lisans dosyası tanınmayan bir anahtarla imzalanmış.");
    public static readonly Error BadSignature = new("license.bad_signature", "Lisans dosyasının imzası geçersiz. Dosya değiştirilmiş olabilir.");

    public static Error WrongProduct(string licenseProduct, string appProduct) =>
        new("license.wrong_product", $"Bu lisans {LicenseProducts.Name(licenseProduct)} içindir; {LicenseProducts.Name(appProduct)} için kullanılamaz.");
}

/// <summary>
/// Vernuko programs, as written in the signed license ("prd"). The license server sells each license for one program
/// and every program accepts only its own. <see cref="Randevu"/> keeps the first program's id: licenses sold and apps
/// installed before there were other programs carry it.
/// </summary>
public static class LicenseProducts
{
    public const string Randevu = "vernuko-desktop";
    public const string Stok = "vernuko-stok";
    public const string TeknikServis = "vernuko-teknik-servis";

    public static string Name(string? product) => product switch
    {
        Randevu => "Vernuko Randevu",
        Stok => "Vernuko Stok",
        TeknikServis => "Vernuko Teknik Servis",
        null or "" => "bilinmeyen bir program",
        _ => product,
    };
}
