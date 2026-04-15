using System.Security.Cryptography;
using System.Text;

namespace EduConnect.Api.Common.Logging;

public static class LogRedaction
{
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "unknown";

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return $"***{digits}";

        return $"***{digits[^4..]}";
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "unknown";

        var at = email.IndexOf('@');
        if (at <= 0)
            return "***";

        var domain = email[(at + 1)..];
        return $"***@{domain}";
    }

    public static string Sha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
