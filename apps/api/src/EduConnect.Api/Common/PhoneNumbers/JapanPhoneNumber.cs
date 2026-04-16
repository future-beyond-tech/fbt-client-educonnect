namespace EduConnect.Api.Common.PhoneNumbers;

public static class JapanPhoneNumber
{
    public const string CountryCode = "+81";
    public const int LocalDigitsLength = 11;
    public const string ValidationMessage = "Phone number must be exactly 11 digits for Japan (+81).";

    public static string DigitsOnly(string? phone)
    {
        return new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    public static bool IsValidInput(string? phone)
    {
        var digits = DigitsOnly(phone);

        return digits.Length == LocalDigitsLength
            || (digits.StartsWith("81", StringComparison.Ordinal) &&
                digits.Length == LocalDigitsLength + 2);
    }

    public static string NormalizeUserInput(string phone)
    {
        var digits = DigitsOnly(phone);

        if (digits.Length == LocalDigitsLength)
        {
            return digits;
        }

        if (digits.StartsWith("81", StringComparison.Ordinal) &&
            digits.Length == LocalDigitsLength + 2)
        {
            return digits[2..];
        }

        throw new ArgumentException(ValidationMessage, nameof(phone));
    }

    public static string NormalizeLegacyStoredValue(string? phone)
    {
        var digits = DigitsOnly(phone);

        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        if (digits.Length == LocalDigitsLength)
        {
            return digits;
        }

        if (digits.StartsWith("81", StringComparison.Ordinal) &&
            digits.Length == LocalDigitsLength + 2)
        {
            return digits[2..];
        }

        if (digits.StartsWith("91", StringComparison.Ordinal) && digits.Length == 12)
        {
            return $"0{digits[2..]}";
        }

        if (digits.Length == 10)
        {
            return $"0{digits}";
        }

        return digits;
    }

    public static string NormalizeSearchTerm(string? phone)
    {
        var digits = DigitsOnly(phone);

        if (digits.StartsWith("81", StringComparison.Ordinal) && digits.Length > 2)
        {
            return digits[2..];
        }

        return digits;
    }
}
