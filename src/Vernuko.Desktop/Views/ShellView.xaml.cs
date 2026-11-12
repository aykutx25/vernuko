using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Vernuko.Desktop.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var window = Window.GetWindow(this);
            if (window is not null)
            {
                window.PreviewKeyDown += OnWindowKeyDown;
                Unloaded += (_, _) => window.PreviewKeyDown -= OnWindowKeyDown;
            }
        };
    }

    /// <summary>Ctrl+K focuses the global search from anywhere.</summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            GlobalSearch.Focus();
            GlobalSearch.SelectAll();
            e.Handled = true;
        }
    }
}
