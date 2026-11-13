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

    /// <param name="publicKeys">Key id → SubjectPublicKeyInfo (DER) bytes.</param>
    public LicenseVerifier(IReadOnlyDictionary<string, byte[]> publicKeys)
    {
        _publicKeys = publicKeys;
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
            return doc is null || doc.FormatVersion != 1 ? LicenseErrors.Malformed : doc;
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
}
