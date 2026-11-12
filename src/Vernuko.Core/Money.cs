using System.Globalization;

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

    /// <summary>Parses user input in Turkish format ("1.250,50", "1250,5", "1250").</summary>
    public static Result<Money> Parse(string? text, string currency = "TRY")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("money.empty", "Tutar boş olamaz.");
        }

        var cleaned = text.Replace("₺", string.Empty, StringComparison.Ordinal)
            .Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (!decimal.TryParse(cleaned, NumberStyles.Number, TurkishText.Culture, out var value))
        {
            return Error.Validation("money.invalid", "Tutar geçersiz. Örnek: 1.250,50");
        }

        if (value < 0)
        {
            return Error.Validation("money.negative", "Tutar negatif olamaz.");
        }

        if (decimal.Round(value, 2) != value)
        {
            return Error.Validation("money.precision", "Tutar en fazla iki ondalık basamak içerebilir.");
        }

        return FromMajor(value, currency);
    }

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
