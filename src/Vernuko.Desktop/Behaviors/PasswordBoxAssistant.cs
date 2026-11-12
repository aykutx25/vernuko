using System.Windows;
using System.Windows.Controls;

namespace Vernuko.Desktop.Behaviors;

/// <summary>
/// Makes PasswordBox.Password bindable. The password lives in the view model only as long as the form is open;
/// view models clear it right after use.
/// The default value is null on purpose: the view model starts with "", so the first binding transfer is a real
/// change (null → "") and hooks PasswordChanged. With an "" default the callback never ran and typed passwords
/// never reached the view model (found on the first Windows run: every login counted as a wrong password).
/// </summary>
public static class PasswordBoxAssistant
{
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword", typeof(string), typeof(PasswordBoxAssistant),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty UpdatingProperty = DependencyProperty.RegisterAttached(
        "Updating", typeof(bool), typeof(PasswordBoxAssistant));

    public static string GetBoundPassword(DependencyObject d) => (string?)d.GetValue(BoundPasswordProperty) ?? string.Empty;

    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordChanged;
        var value = e.NewValue as string ?? string.Empty;
        if (!(bool)box.GetValue(UpdatingProperty) && box.Password != value)
        {
            box.Password = value;
        }

        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(UpdatingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(UpdatingProperty, false);
    }
}

/// <summary>Focuses an element when it is loaded (FocusOnLoad="True").</summary>
public static class FocusBehavior
{
    public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.RegisterAttached(
        "FocusOnLoad", typeof(bool), typeof(FocusBehavior), new PropertyMetadata(false, OnChanged));

    public static bool GetFocusOnLoad(DependencyObject d) => (bool)d.GetValue(FocusOnLoadProperty);

    public static void SetFocusOnLoad(DependencyObject d, bool value) => d.SetValue(FocusOnLoadProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe && e.NewValue is true)
        {
            fe.Loaded += (_, _) => fe.Focus();
        }
    }
}
