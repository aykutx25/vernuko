namespace Vernuko.Presentation.Common;

/// <summary>Platform dialogs. Implemented by the WPF app; replaced by fakes in tests.</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Evet", string cancelText = "Vazgeç", bool destructive = false);

    Task ShowMessageAsync(string title, string message);

    /// <summary>Asks for a line of text; null when cancelled.</summary>
    Task<string?> PromptAsync(string title, string message, string? initialValue = null, bool required = true);

    Task<string?> PickFolderAsync(string title, string? initialFolder);

    Task<string?> PickOpenFileAsync(string title, string filter);

    Task<string?> PickSaveFileAsync(string title, string defaultFileName, string filter);
}

public interface IPlatformServices
{
    void OpenUri(Uri uri);

    void OpenFolder(string path);

    void CopyToClipboard(string text);

    Task WriteTextFileAsync(string path, string content);

    Task<string> ReadTextFileAsync(string path);

    Task<byte[]> ReadFileBytesAsync(string path);

    Task WriteFileBytesAsync(string path, byte[] content);
}

public sealed record UpdateCheck(bool Available, string? NewVersion, string Message);

/// <summary>
/// Application updates (Velopack on Windows). Updates are only offered when the app was installed with the setup
/// package and an HTTPS update source was configured at build time; otherwise <see cref="UnavailableReason"/> says why.
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }

    /// <summary>Null when updates can be checked; otherwise a Turkish explanation.</summary>
    string? UnavailableReason { get; }

    Task<UpdateCheck> CheckAsync(CancellationToken ct = default);

    /// <summary>Downloads the update found by the last check. Progress is 0–100.</summary>
    Task<bool> DownloadAsync(IProgress<int>? progress, CancellationToken ct = default);

    /// <summary>Closes the app, applies the downloaded update and starts the new version.</summary>
    void ApplyAndRestart();
}
