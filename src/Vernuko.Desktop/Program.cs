using Velopack;

namespace Vernuko.Desktop;

/// <summary>
/// Custom entry point: Velopack must handle its install/update/uninstall hooks before any WPF or database code
/// runs. Hooks only manage shortcuts and the program folder; customer data in %LOCALAPPDATA%\Vernuko is never touched.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
