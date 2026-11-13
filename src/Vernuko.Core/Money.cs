using System.Globalization;
using System.Text.RegularExpressions;

namespace Vernuko.Core;

/// <summary>
/// Money in minor units (kuruş/cent) to avoid floating point errors and allow exact SUM() in SQLite.
/// Phase 1 is single-currency per business; the currency code is kept for future international use.
/// </summary>
public readonly record struct Money(long Minor, string Currency = "TRY")
{
    public static Money Zero(string currency = "TRY") => new(0, currency);

    public static Money FromMajor(decimal amount, string currency = "TRY") =>
        new(checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)), currency);

    public decimal Major => Minor / 100m;

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(checked(a.Minor + b.Minor), a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(checked(a.Minor - b.Minor), a.Currency);
    }

    /// <summary>
    /// Parses user input in Turkish format ("1.250,50", "1250,5", "1250"). A dot is a thousands separator only in
    /// thousands positions; a single dot followed by one or two digits ("12.50") is read as the decimal point, as
    /// many people type it that way. Anything else ("1.5.0", "12.5000") is rejected instead of guessed: a dot
    /// silently ignored once turned "12.50" into ₺1.250,00.
    /// </summary>
    public static Result<Money> Parse(string? text, string currency = "TRY")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("money.empty", "Tutar boş olamaz.");
        }

        var cleaned = text.Replace("₺", string.Empty, StringComparison.Ordinal)
            .Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Trim();
        var negative = cleaned.StartsWith('-');
        var digits = negative ? cleaned[1..] : cleaned;
        string normalized;
        if (TurkishNumber.IsMatch(digits))
        {
            normalized = digits.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else if (DotDecimal.IsMatch(digits))
        {
            normalized = digits;
        }
        else
        {
            return Error.Validation("money.invalid", "Tutar geçersiz. Örnek: 1.250,50");
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return Error.Validation("money.invalid", "Tutar geçersiz. Örnek: 1.250,50");
        }

        if (negative && value != 0)
        {
            return Error.Validation("money.negative", "Tutar negatif olamaz.");
        }

        if (decimal.Round(value, 2) != value)
        {
            return Error.Validation("money.precision", "Tutar en fazla iki ondalık basamak içerebilir.");
        }

        return FromMajor(value, currency);
    }

    /// <summary>"1250", "1250,5", "1.250", "1.250,50", "1.250.000".</summary>
    private static readonly Regex TurkishNumber = new(@"^(\d+|\d{1,3}(\.\d{3})+)(,\d+)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>"12.50", "1.5": a dot used as the decimal point.</summary>
    private static readonly Regex DotDecimal = new(@"^\d+\.\d{1,2}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public string ToDisplayString()
    {
        var symbol = Currency == "TRY" ? "₺" : Currency + " ";
        return symbol + Major.ToString("#,##0.00", TurkishText.Culture);
    }

    public override string ToString() => ToDisplayString();

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Currency mismatch: {a.Currency} vs {b.Currency}.");
        }
    }
}
