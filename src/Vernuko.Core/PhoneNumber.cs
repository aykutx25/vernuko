using System.Text;

namespace Vernuko.Core;

/// <summary>
/// Phone number normalised to E.164 (e.g. +905324567890). Turkish local formats (0532…, 532…, 90532…) are accepted.
/// International numbers must be entered with a leading "+" or "00".
/// </summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string e164)
    {
        E164 = e164;
    }

    public string E164 { get; }

    /// <summary>Digits only, without "+": the format used by wa.me links and most SMS APIs.</summary>
    public string Digits => E164[1..];

    public bool IsTurkish => E164.StartsWith("+90", StringComparison.Ordinal);

    public static Result<PhoneNumber> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("phone.empty", "Telefon numarası boş olamaz.");
        }

        var trimmed = input.Trim();
        var international = trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal);
        var digits = new StringBuilder();
        foreach (var ch in trimmed)
        {
            if (char.IsAsciiDigit(ch))
            {
                digits.Append(ch);
            }
            else if (ch is not (' ' or '-' or '(' or ')' or '.' or '+' or '/'))
            {
                return Error.Validation("phone.invalid_chars", "Telefon numarası yalnızca rakam içermelidir.");
            }
        }

        var d = digits.ToString();
        if (international)
        {
            if (d.StartsWith("00", StringComparison.Ordinal))
            {
                d = d[2..];
            }

            if (d.StartsWith("90", StringComparison.Ordinal))
            {
                return ParseTurkishNational(d[2..]);
            }

            // E.164 allows at most 15 digits; shortest real numbers are around 8.
            if (d.Length is < 8 or > 15 || d[0] == '0')
            {
                return Error.Validation("phone.invalid", "Uluslararası telefon numarası geçersiz.");
            }

            return new PhoneNumber("+" + d);
        }

        if (d.Length == 12 && d.StartsWith("90", StringComparison.Ordinal))
        {
            return ParseTurkishNational(d[2..]);
        }

        if (d.Length == 11 && d[0] == '0')
        {
            return ParseTurkishNational(d[1..]);
        }

        return ParseTurkishNational(d);
    }

    private static Result<PhoneNumber> ParseTurkishNational(string national)
    {
        // Turkish national significant numbers are 10 digits; mobiles start with 5, landlines with 2/3/4, 850 etc. with 8.
        if (national.Length != 10 || national[0] is '0' or '1' or '6' or '7' or '9')
        {
            return Error.Validation("phone.invalid_tr", "Telefon numarası 10 haneli olmalıdır (ör. 0532 123 45 67).");
        }

        return new PhoneNumber("+90" + national);
    }

    /// <summary>Human friendly format. Turkish numbers: "0532 456 78 90".</summary>
    public string ToDisplayString()
    {
        if (IsTurkish && E164.Length == 13)
        {
            var n = E164[3..];
            return $"0{n[..3]} {n[3..6]} {n[6..8]} {n[8..]}";
        }

        return E164;
    }

    public override string ToString() => E164;
}
