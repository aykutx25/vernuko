using System.Windows;
using System.Windows.Controls;

namespace Vernuko.Desktop.Behaviors;

/// <summary>
/// DataGrid text cells end in "…" when the column is too narrow (the default cell text just stops mid-letter),
/// and show the whole text as a tooltip. Set for every DataGrid by the theme; columns with their own ElementStyle
/// keep it.
/// </summary>
public static class DataGridAssist
{
    public static readonly DependencyProperty TrimTextProperty = DependencyProperty.RegisterAttached(
        "TrimText", typeof(bool), typeof(DataGridAssist), new PropertyMetadata(false, OnTrimTextChanged));

    private static readonly Style TrimmedCell = CreateTrimmedCell();

    public static bool GetTrimText(DependencyObject d) => (bool)d.GetValue(TrimTextProperty);

    public static void SetTrimText(DependencyObject d, bool value) => d.SetValue(TrimTextProperty, value);

    private static Style CreateTrimmedCell()
    {
        var style = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle);
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { RelativeSource = System.Windows.Data.RelativeSource.Self }));
        var empty = new Trigger { Property = TextBlock.TextProperty, Value = string.Empty };
        empty.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, null));
        style.Triggers.Add(empty);
        style.Seal();
        return style;
    }

    private static void OnTrimTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataGrid grid && e.NewValue is true)
        {
            grid.Loaded += (_, _) => Apply(grid);
        }
    }

    private static void Apply(DataGrid grid)
    {
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
        {
            if (ReferenceEquals(column.ElementStyle, DataGridTextColumn.DefaultElementStyle))
            {
                column.ElementStyle = TrimmedCell;
            }
        }
    }
}

/// <summary>Scrolls an element into view when the bound value turns true (the selected menu item under a short window).</summary>
public static class ScrollAssist
{
    public static readonly DependencyProperty BringIntoViewWhenProperty = DependencyProperty.RegisterAttached(
        "BringIntoViewWhen", typeof(bool), typeof(ScrollAssist), new PropertyMetadata(false, OnChanged));

    public static bool GetBringIntoViewWhen(DependencyObject d) => (bool)d.GetValue(BringIntoViewWhenProperty);

    public static void SetBringIntoViewWhen(DependencyObject d, bool value) => d.SetValue(BringIntoViewWhenProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is true)
        {
            if (element.IsLoaded)
            {
                element.BringIntoView();
            }
            else
            {
                element.Loaded += (_, _) => element.BringIntoView();
            }
        }
    }
}
