namespace EduConnect.Api.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static void ValidateEnvironment()
    {
        var requiredVars = new[]
        {
            "DATABASE_URL",
            "JWT_SECRET",
            "JWT_ISSUER",
            "JWT_AUDIENCE",
            "PIN_MIN_LENGTH",
            "PIN_MAX_LENGTH",
            "CORS_ALLOWED_ORIGINS",
            "RATE_LIMIT_API_PER_USER_PER_MINUTE",
            "RESEND_API_KEY",
            "RESEND_FROM_EMAIL",
            "NEXT_PUBLIC_APP_URL"
        };
        var missing = requiredVars.Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))).ToList();

        if (missing.Count != 0)
        {
            throw new InvalidOperationException(
                $"Missing required environment variables: {string.Join(", ", missing)}");
        }

        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (!string.IsNullOrWhiteSpace(jwtSecret) && jwtSecret.Length < 64)
        {
            throw new InvalidOperationException("JWT_SECRET must be at least 64 characters long.");
        }
    }
}
