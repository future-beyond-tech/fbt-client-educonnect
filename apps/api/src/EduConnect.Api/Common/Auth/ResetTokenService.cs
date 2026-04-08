using System.Security.Cryptography;
using System.Text;

namespace EduConnect.Api.Common.Auth;

/// <summary>
/// Generates cryptographically random one-time reset tokens and computes
/// SHA-256 hashes of them. The raw token is delivered to the user via email
/// (Resend); only the hash is persisted in <c>auth_reset_tokens</c>.
/// </summary>
public class ResetTokenService
{
    private const int TokenByteLength = 32; // 256 bits

    /// <summary>
    /// Generates a fresh cryptographically random URL-safe token.
    /// </summary>
    public string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Computes a SHA-256 hex hash of the supplied raw token.
    /// </summary>
    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Reset token cannot be empty.", nameof(rawToken));
        }

        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
