using System.Runtime.CompilerServices;

namespace Vernuko.Core;

public static class Guard
{
    public static DateTime Utc(DateTime value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Value must be a UTC DateTime.", name);
        }

        return value;
    }

    public static string NotBlank(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", name);
        }

        return value;
    }
}
