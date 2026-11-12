using System.Reflection;
using Vernuko.Application.Services;

namespace Vernuko.Desktop.Services;

/// <summary>
/// Values baked into the build by the release pipeline (assembly metadata), so a local setting cannot point the
/// app at another license server or make it trust another signing key.
/// </summary>
internal static class BuildSettings
{
    public static LicensingOptions Licensing()
    {
        var url = Metadata("VernukoLicenseServerUrl");
        return new LicensingOptions
        {
            PublicKeys = LicenseKeys(),
            ServerUrl = Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? u : null,
        };
    }

    /// <summary>"kid:base64|kid2:base64" → key id → SubjectPublicKeyInfo bytes. Malformed entries are skipped.</summary>
    public static IReadOnlyDictionary<string, byte[]> LicenseKeys()
    {
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in (Metadata("VernukoLicenseKeys") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = entry.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            try
            {
                keys[entry[..colon]] = Convert.FromBase64String(entry[(colon + 1)..]);
            }
            catch (FormatException)
            {
            }
        }

        return keys;
    }

    private static string? Metadata(string key) =>
        typeof(BuildSettings).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
}
