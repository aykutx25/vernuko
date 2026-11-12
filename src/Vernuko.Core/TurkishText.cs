using System.Globalization;
using System.Text;

namespace Vernuko.Core;

/// <summary>
/// Turkish-aware text helpers. Search keys are lower-cased with tr-TR rules (I→ı, İ→i) and then folded to ASCII
/// so that "ayse", "AYŞE" and "Ayşe" all match, and "isik" matches "IŞIK".
/// </summary>
public static class TurkishText
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    public static string ToSearchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.Trim().ToLower(Culture);
        var sb = new StringBuilder(lower.Length);
        var lastWasSpace = false;
        foreach (var ch in lower)
        {
            var folded = ch switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ı' => 'i',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                'â' => 'a',
                'î' => 'i',
                'û' => 'u',
                _ => ch,
            };

            if (char.IsWhiteSpace(folded))
            {
                if (!lastWasSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            // Drop combining marks (e.g. the dot produced by some "İ" lower-casing paths).
            if (CharUnicodeInfo.GetUnicodeCategory(folded) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(folded);
            lastWasSpace = false;
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Title-cases a personal name using Turkish rules ("ayşe IŞIK" → "Ayşe Işık").</summary>
    public static string ToNameCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i].ToLower(Culture);
            parts[i] = p.Length == 1
                ? p.ToUpper(Culture)
                : char.ToUpper(p[0], Culture) + p[1..];
        }

        return string.Join(' ', parts);
    }
}
