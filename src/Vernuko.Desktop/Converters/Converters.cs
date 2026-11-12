using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Vernuko.Core;

namespace Vernuko.Desktop.Converters;

/// <summary>true → Visible. Parameter "invert" flips the logic. Also treats non-null objects, non-empty strings and non-zero numbers as true.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var truthy = value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            long l => l != 0,
            ICollection c => c.Count > 0,
            _ => true,
        };
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            truthy = !truthy;
        }

        return truthy ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Two-way enum ↔ radio button: IsChecked="{Binding View, Converter={StaticResource EnumEquals}, ConverterParameter=Day}".</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string p && string.Equals(value.ToString(), p, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string p ? Enum.Parse(targetType, p) : Binding.DoNothing;
}

public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        long minor => new Money(minor).ToDisplayString(),
        int minor => new Money(minor).ToDisplayString(),
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Formats DateTime values: parameter "time", "date", "datetime", "long", or "utc-datetime" (UTC → local).</summary>
public sealed class DateFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d)
        {
            value = d.ToDateTime(TimeOnly.MinValue);
        }

        if (value is not DateTime dt)
        {
            return string.Empty;
        }

        var tr = TurkishText.Culture;
        return (parameter as string) switch
        {
            "time" => dt.ToString("HH:mm", tr),
            "date" => dt.ToString("dd.MM.yyyy", tr),
            "long" => dt.ToString("d MMMM yyyy, dddd", tr),
            "utc-datetime" => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy HH:mm", tr),
            "utc-date" => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy", tr),
            _ => dt.ToString("dd.MM.yyyy HH:mm", tr),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Looks up an icon geometry by resource key (used for data-driven navigation icons).</summary>
public sealed class IconKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? System.Windows.Application.Current.TryFindResource(key) as Geometry : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Percentage (0–100) → arc geometry (centre 55,55, radius 50) for the small donut on the summary card.</summary>
public sealed class PercentArcConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value is int p ? Math.Clamp(p, 0, 100) : 0;
        if (percent == 0)
        {
            return Geometry.Empty;
        }

        if (percent >= 100)
        {
            return new EllipseGeometry(new Point(55, 55), 50, 50);
        }

        var angle = percent / 100.0 * 2 * Math.PI;
        var x = 55 + (50 * Math.Sin(angle));
        var y = 55 - (50 * Math.Cos(angle));
        var large = percent > 50 ? 1 : 0;
        return Geometry.Parse(string.Create(CultureInfo.InvariantCulture, $"M 55 5 A 50 50 0 {large} 1 {x:0.##} {y:0.##}"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Packs several bindings into an object[] command parameter.</summary>
public sealed class ParametersConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) => values.ToArray();

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
