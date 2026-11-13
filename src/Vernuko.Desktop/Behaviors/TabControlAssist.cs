using System.Windows;
using System.Windows.Controls;

namespace Vernuko.Desktop.Behaviors;

/// <summary>
/// Keeps a TabControl on a visible tab. WPF selects the first tab even when it is collapsed (e.g. "İşletme" in Ayarlar
/// for a user without the permission), and then shows its content with no header. Set for every TabControl by the
/// theme.
/// </summary>
public static class TabControlAssist
{
    public static readonly DependencyProperty SelectVisibleTabProperty = DependencyProperty.RegisterAttached(
        "SelectVisibleTab", typeof(bool), typeof(TabControlAssist), new PropertyMetadata(false, OnSelectVisibleTabChanged));

    public static bool GetSelectVisibleTab(DependencyObject d) => (bool)d.GetValue(SelectVisibleTabProperty);

    public static void SetSelectVisibleTab(DependencyObject d, bool value) => d.SetValue(SelectVisibleTabProperty, value);

    private static void OnSelectVisibleTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TabControl tabs || e.NewValue is not true)
        {
            return;
        }

        tabs.Loaded += (_, _) =>
        {
            foreach (var item in tabs.Items.OfType<TabItem>())
            {
                item.IsVisibleChanged += (_, _) => Fix(tabs);
            }

            Fix(tabs);
        };
        tabs.SelectionChanged += (_, args) =>
        {
            if (ReferenceEquals(args.OriginalSource, tabs))
            {
                Fix(tabs);
            }
        };
    }

    private static void Fix(TabControl tabs)
    {
        if (!tabs.IsLoaded || tabs.SelectedItem is TabItem { Visibility: Visibility.Visible })
        {
            return;
        }

        if (tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible) is { } first)
        {
            tabs.SelectedItem = first;
        }
    }
}
