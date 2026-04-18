using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EduConnect.Api.Common.Auth;

public class JwtTokenService
{
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration config, ILogger<JwtTokenService> logger)
    {
        _jwtSecret = config["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET is missing");
        _jwtIssuer = config["JWT_ISSUER"] ?? throw new InvalidOperationException("JWT_ISSUER is missing");
        _jwtAudience = config["JWT_AUDIENCE"] ?? throw new InvalidOperationException("JWT_AUDIENCE is missing");
        _logger = logger;
    }

    public string GenerateAccessToken(
        Guid userId,
        Guid schoolId,
        string role,
        string name,
        int expirationMinutes = 15,
        bool mustChangePassword = false)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("userId", userId.ToString()),
            new Claim("schoolId", schoolId.ToString()),
            new Claim("role", role),
            new Claim("name", name),
            new Claim("must_change_password", mustChangePassword ? "true" : "false")
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }
        return Convert.ToBase64String(randomNumber);
    }

    public string BuildRefreshToken(Guid tokenId, string secret)
    {
        return $"{tokenId:N}.{secret}";
    }

    public bool TryParseRefreshToken(string token, out Guid tokenId, out string secret)
    {
        tokenId = Guid.Empty;
        secret = string.Empty;

        var separatorIndex = token.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
        {
            return false;
        }

        var idPart = token[..separatorIndex];
        var secretPart = token[(separatorIndex + 1)..];

        if (!Guid.TryParseExact(idPart, "N", out tokenId) || string.IsNullOrWhiteSpace(secretPart))
        {
            tokenId = Guid.Empty;
            return false;
        }

        secret = secretPart;
        return true;
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var tokenHandler = new JwtSecurityTokenHandler();

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = true,
                ValidIssuer = _jwtIssuer,
                ValidateAudience = true,
                ValidAudience = _jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }
}
