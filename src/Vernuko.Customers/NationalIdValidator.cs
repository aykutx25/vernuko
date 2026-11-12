namespace Vernuko.Customers;

/// <summary>T.C. kimlik numarası checksum validation (11 digits, first digit non-zero, two check digits).</summary>
public static class NationalIdValidator
{
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 11 || value[0] == '0' || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var d = value.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var tenth = (((odd * 7) - even) % 10 + 10) % 10;
        if (tenth != d[9])
        {
            return false;
        }

        var eleventh = d.Take(10).Sum() % 10;
        return eleventh == d[10];
    }
}
