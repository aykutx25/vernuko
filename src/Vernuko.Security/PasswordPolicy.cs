using Vernuko.Core;

namespace Vernuko.Security;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890", "password", "parola123", "sifre123", "şifre123", "qwerty123",
        "11111111", "00000000", "abcd1234", "vernuko1", "vernuko123", "admin123", "123123123", "asdf1234",
    };

    public static Result Validate(string? password, string? username = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return Error.Validation("password.length", $"Parola en az {MinLength} karakter olmalıdır.");
        }

        if (password.Distinct().Count() < 4)
        {
            return Error.Validation("password.weak", "Parola çok basit. Farklı karakterler kullanın.");
        }

        if (CommonPasswords.Contains(password))
        {
            return Error.Validation("password.common", "Bu parola çok yaygın kullanılıyor. Lütfen farklı bir parola seçin.");
        }

        if (!string.IsNullOrEmpty(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("password.username", "Parola kullanıcı adını içermemelidir.");
        }

        return Result.Success();
    }
}
