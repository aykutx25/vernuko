namespace Vernuko.Customers;

/// <summary>
/// Vergi Kimlik Numarası (10 digits) check digit, as published by the Revenue Administration's algorithm:
/// for the first nine digits, t = (digit + 9 − i) mod 10, v = t·2^(9−i) mod 9 (9 when t ≠ 0 and v = 0);
/// the last digit is (10 − Σv mod 10) mod 10.
/// </summary>
public static class TaxNumberValidator
{
    public static bool IsValidVkn(string? value)
    {
        if (value is null || value.Length != 10 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var t = (value[i] - '0' + 9 - i) % 10;
            var v = t * (1 << (9 - i)) % 9;
            sum += t != 0 && v == 0 ? 9 : v;
        }

        return (10 - (sum % 10)) % 10 == value[9] - '0';
    }
}
