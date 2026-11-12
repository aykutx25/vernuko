using System.Reflection;
using Vernuko.Application.Common;

namespace Vernuko.Infrastructure;

/// <summary>
/// Data lives under %LOCALAPPDATA%\Vernuko (per Windows user, not roaming). Demo mode uses a separate folder so
/// demo data can never mix with real records.
/// </summary>
public sealed class AppEnvironment : IAppEnvironment
{
    public AppEnvironment(string dataDirectory, bool isDemo)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        IsDemo = isDemo;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(SafetyDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public string DataDirectory { get; }

    public bool IsDemo { get; }

    public string DatabasePath => Path.Combine(DataDirectory, IsDemo ? "vernuko-demo.db" : "vernuko.db");

    public string SafetyDirectory => Path.Combine(DataDirectory, "safety");

    public string TempDirectory => Path.Combine(DataDirectory, "temp");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public string DefaultBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify) is { Length: > 0 } docs ? docs : DataDirectory,
        IsDemo ? "Vernuko Demo Yedekler" : "Vernuko Yedekler");

    public string AppVersion => typeof(AppEnvironment).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static AppEnvironment CreateDefault(bool isDemo)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        return new AppEnvironment(Path.Combine(root, "Vernuko", isDemo ? "Demo" : "Data"), isDemo);
    }

    /// <summary>Removes temp leftovers of interrupted operations (may contain decrypted copies).</summary>
    public void CleanTemp()
    {
        foreach (var dir in Directory.EnumerateDirectories(TempDirectory))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
