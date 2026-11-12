using System.Windows;

namespace Vernuko.Desktop;

public partial class DialogWindow : Window
{
    private readonly bool _required;

    public DialogWindow(string title, string message, string okText, string? cancelText, bool destructive, bool prompt, string? initial, bool required)
    {
        InitializeComponent();
        _required = required;
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        if (destructive)
        {
            OkButton.Style = (Style)FindResource("DangerButton");
        }

        if (cancelText is null)
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelButton.Content = cancelText;
        }

        if (prompt)
        {
            Input.Visibility = Visibility.Visible;
            Input.Text = initial ?? string.Empty;
            Loaded += (_, _) =>
            {
                Input.Focus();
                Input.SelectAll();
            };
        }
    }

    public string InputText => Input.Text.Trim();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Input.Visibility == Visibility.Visible && _required && string.IsNullOrWhiteSpace(Input.Text))
        {
            Input.Focus();
            return;
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
