using System.Text.Json;

namespace Vernuko.Application.Services;

public enum CloudProvider
{
    OneDrive,
    GoogleDrive,
    Dropbox,
    Other,
}

public sealed record CloudFolderCandidate(CloudProvider Provider, string Path)
{
    public string ProviderName => CloudFolderLocator.ProviderName(Provider);

    public string Label => $"{ProviderName} — {Path}";
}

/// <summary>
/// Finds the folders that the OneDrive, Google Drive for desktop and Dropbox clients keep in sync with the user's
/// own cloud account. Vernuko only copies the already-encrypted .vbk file there; the provider's own client uploads
/// it. No API keys, no OAuth app registration, and the backup lands in the customer's account, not ours.
/// </summary>
public sealed class CloudFolderLocator
{
    /// <summary>Localised names of Google Drive's "My Drive" folder (Windows display language dependent).</summary>
    private static readonly string[] GoogleMyDriveNames = ["My Drive", "Drive'ım", "Meine Ablage", "Mi unidad", "Mon Drive", "Il mio Drive"];

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly string _userProfile;
    private readonly string _localAppData;
    private readonly string _appData;
    private readonly Func<IEnumerable<(string Root, string? Label)>> _drives;

    public CloudFolderLocator()
        : this(
            Environment.GetEnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            SystemDrives)
    {
    }

    public CloudFolderLocator(Func<string, string?> getEnvironmentVariable, string userProfile, string localAppData, string appData, Func<IEnumerable<(string Root, string? Label)>> drives)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
        _userProfile = userProfile;
        _localAppData = localAppData;
        _appData = appData;
        _drives = drives;
    }

    public static string ProviderName(CloudProvider provider) => provider switch
    {
        CloudProvider.OneDrive => "OneDrive",
        CloudProvider.GoogleDrive => "Google Drive",
        CloudProvider.Dropbox => "Dropbox",
        _ => "Başka bir eşitleme klasörü",
    };

    public IReadOnlyList<CloudFolderCandidate> Detect()
    {
        var found = new List<CloudFolderCandidate>();
        void Add(CloudProvider provider, string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
                && !found.Any(f => string.Equals(System.IO.Path.GetFullPath(f.Path), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(new CloudFolderCandidate(provider, System.IO.Path.GetFullPath(path)));
            }
        }

        // OneDrive sets these for the signed-in personal / work account.
        Add(CloudProvider.OneDrive, _getEnvironmentVariable("OneDriveConsumer"));
        Add(CloudProvider.OneDrive, _getEnvironmentVariable("OneDriveCommercial"));
        Add(CloudProvider.OneDrive, _getEnvironmentVariable("OneDrive"));

        // Google Drive for desktop: a virtual drive labelled "Google Drive" with a localised "My Drive" folder,
        // or (mirror mode / old Backup and Sync) a folder in the user profile.
        foreach (var (root, label) in SafeDrives())
        {
            if (string.Equals(label, "Google Drive", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var name in GoogleMyDriveNames)
                {
                    Add(CloudProvider.GoogleDrive, System.IO.Path.Combine(root, name));
                }
            }
        }

        foreach (var name in GoogleMyDriveNames.Append("Google Drive"))
        {
            Add(CloudProvider.GoogleDrive, System.IO.Path.Combine(_userProfile, name));
        }

        // Dropbox writes its folder locations to info.json.
        foreach (var info in new[] { System.IO.Path.Combine(_localAppData, "Dropbox", "info.json"), System.IO.Path.Combine(_appData, "Dropbox", "info.json") })
        {
            if (File.Exists(info))
            {
                try
                {
                    foreach (var path in ParseDropboxInfo(File.ReadAllText(info)))
                    {
                        Add(CloudProvider.Dropbox, path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                }
            }
        }

        Add(CloudProvider.Dropbox, System.IO.Path.Combine(_userProfile, "Dropbox"));
        return found;
    }

    /// <summary>Reads the "path" of every account in Dropbox's info.json ({"personal":{"path":…},"business":{…}}).</summary>
    public static IReadOnlyList<string> ParseDropboxInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var paths = new List<string>();
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return paths;
        }

        foreach (var account in doc.RootElement.EnumerateObject())
        {
            if (account.Value.ValueKind == JsonValueKind.Object && account.Value.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private IEnumerable<(string Root, string? Label)> SafeDrives()
    {
        try
        {
            return _drives().ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<(string Root, string? Label)> SystemDrives()
    {
        foreach (var d in DriveInfo.GetDrives())
        {
            string? label = null;
            try
            {
                if (d.IsReady)
                {
                    label = d.VolumeLabel;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            yield return (d.RootDirectory.FullName, label);
        }
    }
}
