namespace Vernuko.Infrastructure;

/// <summary>Names that must stay stable across versions.</summary>
public static class AppIdentity
{
    /// <summary>
    /// Velopack package id. The installer puts the program under %LOCALAPPDATA%\VernukoApp and REMOVES that folder on
    /// uninstall, so it must never be the data folder (or a parent of it). Customer data lives in
    /// %LOCALAPPDATA%\<see cref="DataFolderName"/> and survives uninstall/reinstall. Never change this after release:
    /// a different id is a different app for the updater.
    /// </summary>
    public const string InstallPackId = "VernukoApp";

    /// <summary>Folder under %LOCALAPPDATA% that holds the database, attachments, logs and safety copies.</summary>
    public const string DataFolderName = "Vernuko";
}
