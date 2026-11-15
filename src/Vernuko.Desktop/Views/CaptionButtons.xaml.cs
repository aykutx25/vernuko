using System.Windows;
using System.Windows.Controls;

namespace Vernuko.Desktop.Views;

/// <summary>Minimize, maximize/restore and close for the window that hosts it (the app draws its own title bar).</summary>
public partial class CaptionButtons : UserControl
{
    public CaptionButtons()
    {
        InitializeComponent();
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    // Closing goes through MainWindow.OnClosing (exit backup) like any other close.
    private void OnClose(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
