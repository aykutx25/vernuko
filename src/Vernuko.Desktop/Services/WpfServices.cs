using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Vernuko.Presentation.Common;

namespace Vernuko.Desktop.Services;

public sealed class WpfDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Evet", string cancelText = "Vazgeç", bool destructive = false)
    {
        var dlg = new DialogWindow(title, message, confirmText, cancelText, destructive, prompt: false, initial: null, required: false) { Owner = ActiveWindow() };
        return Task.FromResult(dlg.ShowDialog() == true);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        var dlg = new DialogWindow(title, message, "Tamam", null, false, prompt: false, initial: null, required: false) { Owner = ActiveWindow() };
        dlg.ShowDialog();
        return Task.CompletedTask;
    }

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null, bool required = true)
    {
        var dlg = new DialogWindow(title, message, "Tamam", "Vazgeç", false, prompt: true, initial: initialValue, required: required) { Owner = ActiveWindow() };
        return Task.FromResult(dlg.ShowDialog() == true ? dlg.InputText : null);
    }

    public Task<string?> PickFolderAsync(string title, string? initialFolder)
    {
        var dlg = new OpenFolderDialog { Title = title, InitialDirectory = initialFolder is not null && Directory.Exists(initialFolder) ? initialFolder : string.Empty };
        return Task.FromResult(Show(dlg) ? dlg.FolderName : null);
    }

    public Task<string?> PickOpenFileAsync(string title, string filter)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return Task.FromResult(Show(dlg) ? dlg.FileName : null);
    }

    public Task<string?> PickSaveFileAsync(string title, string defaultFileName, string filter)
    {
        var dlg = new SaveFileDialog { Title = title, FileName = defaultFileName, Filter = filter, OverwritePrompt = true };
        return Task.FromResult(Show(dlg) ? dlg.FileName : null);
    }

    private static bool Show(CommonDialog dialog) =>
        (ActiveWindow() is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;

    private static Window? ActiveWindow() =>
        System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
}

public sealed class WpfPlatformServices : IPlatformServices
{
    public void OpenUri(Uri uri)
    {
        if (uri.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("Only web links can be opened.", nameof(uri));
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public void OpenFolder(string path) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetFullPath(path)}\"") { UseShellExecute = false });

    public void CopyToClipboard(string text) => Clipboard.SetText(text);

    public Task WriteTextFileAsync(string path, string content) => File.WriteAllTextAsync(path, content, new UTF8Encoding(false));

    public Task<string> ReadTextFileAsync(string path) => File.ReadAllTextAsync(path);

    public Task<byte[]> ReadFileBytesAsync(string path) => File.ReadAllBytesAsync(path);

    public Task WriteFileBytesAsync(string path, byte[] content) => File.WriteAllBytesAsync(path, content);
}
