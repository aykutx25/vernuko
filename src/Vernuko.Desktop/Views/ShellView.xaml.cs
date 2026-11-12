using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vernuko.Presentation;

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
                window.KeyDown += OnWindowKeyDown;
                Unloaded += (_, _) => window.KeyDown -= OnWindowKeyDown;
            }
        };
    }

    /// <summary>
    /// Shortcuts are handled at window level (bubbling KeyDown): they work even when nothing has keyboard focus, and
    /// controls that use a key themselves (an open drop-down closing on Esc, a text box) still get it first.
    /// </summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || DataContext is not ShellViewModel shell || (sender as Window)?.DataContext is RootViewModel { LockScreen: not null })
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (key == Key.K && modifiers == ModifierKeys.Control)
        {
            if (shell.Overlay is null)
            {
                GlobalSearch.Focus();
                GlobalSearch.SelectAll();
                e.Handled = true;
            }

            return;
        }

        Shortcut? shortcut = (key, modifiers) switch
        {
            (Key.N, ModifierKeys.Control) => Shortcut.NewAppointment,
            (Key.L, ModifierKeys.Control) => Shortcut.Lock,
            (Key.S, ModifierKeys.Control) => Shortcut.Save,
            (Key.Escape, ModifierKeys.None) => Shortcut.Close,
            (Key.F5, ModifierKeys.None) => Shortcut.Refresh,
            (Key.Left, ModifierKeys.Alt) => Shortcut.Previous,
            (Key.Right, ModifierKeys.Alt) => Shortcut.Next,
            (Key.T, ModifierKeys.Alt) => Shortcut.Today,
            _ => null,
        };
        if (shortcut is { } s && shell.HandleShortcut(s))
        {
            e.Handled = true;
        }
    }
}
