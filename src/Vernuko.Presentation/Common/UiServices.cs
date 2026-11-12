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
}
