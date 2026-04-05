using BCrypt.Net;

namespace EduConnect.Api.Common.Auth;

public class PinService
{
    private const int WorkFactor = 12;

    public string HashPin(string pin)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(pin, WorkFactor);
    }

    public bool VerifyPin(string pin, string pinHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(pin, pinHash);
        }
        catch
        {
            return false;
        }
    }

    public bool ValidatePinFormat(string pin)
    {
        return !string.IsNullOrWhiteSpace(pin) &&
               pin.Length >= 4 &&
               pin.Length <= 6 &&
               pin.All(char.IsDigit);
    }
}
