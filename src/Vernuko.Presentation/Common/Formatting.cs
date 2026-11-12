using Vernuko.Core;

namespace Vernuko.Presentation.Common;

public static class Formatting
{
    public static string LongDate(DateTime date) => date.ToString("d MMMM yyyy, dddd", TurkishText.Culture);

    public static string LongDate(DateOnly date) => LongDate(date.ToDateTime(TimeOnly.MinValue));

    public static string ShortDate(DateTime date) => date.ToString("dd.MM.yyyy", TurkishText.Culture);

    public static string Time(DateTime date) => date.ToString("HH:mm", TurkishText.Culture);

    public static string Money(long minor) => new Money(minor).ToDisplayString();

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpper(TurkishText.Culture)
            : (parts[0][..1] + parts[^1][..1]).ToUpper(TurkishText.Culture);
    }

    public static string Ago(DateTime utc, DateTime nowUtc)
    {
        var d = nowUtc - utc;
        return d.TotalMinutes < 1 ? "az önce"
            : d.TotalHours < 1 ? $"{(int)d.TotalMinutes} dk önce"
            : d.TotalDays < 1 ? $"{(int)d.TotalHours} saat önce"
            : $"{(int)d.TotalDays} gün önce";
    }

    /// <summary>Parses "14:30", "1430", "14.30" or "9".</summary>
    public static TimeOnly? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var t = text.Trim().Replace('.', ':');
        if (!t.Contains(':', StringComparison.Ordinal) && t.All(char.IsAsciiDigit))
        {
            t = t.Length <= 2 ? t + ":00" : t.Length is 3 or 4 ? t[..^2] + ":" + t[^2..] : t;
        }

        return TimeOnly.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var time) ? time : null;
    }
}

/// <summary>Delays an action until input settles (e.g. search-as-you-type); only the last call runs.</summary>
public sealed class Debouncer(TimeSpan delay)
{
    private int _version;

    public async Task RunAsync(Func<CancellationToken, Task> action)
    {
        var version = Interlocked.Increment(ref _version);
        await Task.Delay(delay);
        if (version == Volatile.Read(ref _version))
        {
            await action(CancellationToken.None);
        }
    }
}

/// <summary>Generic "label + value" option for combo boxes (null value = "all").</summary>
public sealed record Option<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public static class ResultExtensions
{
    public static IReadOnlyList<T> OrEmpty<T>(this Vernuko.Core.Result<IReadOnlyList<T>> result) => result.IsSuccess ? result.Value : [];

    /// <summary>
    /// When a name was typed but not picked from the suggestions: use the single exact match, otherwise explain what
    /// to do (pick from the list, or add the customer first). Returns the customer or a Turkish error message.
    /// </summary>
    public static async Task<(Vernuko.Application.CustomerListItem? Customer, string? Error)> ResolveTypedCustomerAsync(
        Vernuko.Application.Services.ICustomerService customers, string typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return (null, "Müşteri adını veya telefonunu yazıp aşağıda çıkan listeden seçin.");
        }

        var found = (await customers.SearchAsync(typed, take: 6)).OrEmpty();
        var exact = found.Where(c => string.Equals(c.FullName.Trim(), typed.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToList();
        return found.Count == 1 ? (found[0], null)
            : exact.Count == 1 ? (exact[0], null)
            : found.Count == 0 ? (null, $"\"{typed.Trim()}\" adında kayıtlı müşteri yok. Önce Müşteriler ekranından ekleyin, sonra burada seçin.")
            : (null, "Bu isimle birden fazla müşteri var; aşağıdaki listeden doğru kişiyi seçin.");
    }
}
