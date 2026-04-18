namespace EduConnect.Api.Infrastructure.Email;

/// <summary>
/// Resolves the branding URLs used by every email template (public app
/// URL, logo URL) from IConfiguration. Keeps the fallback chain
/// (NEXT_PUBLIC_APP_URL → APP_PUBLIC_URL → http://localhost:3000) in one
/// place so callers don't drift out of sync.
/// </summary>
public static class EmailBranding
{
    /// <summary>Path (relative to the app URL) where the school logo PNG is served.</summary>
    public const string LogoPath = "/ris-logo.png";

    /// <summary>
    /// Return the configured public app URL with any trailing slash stripped.
    /// </summary>
    public static string ResolveAppUrl(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var appUrl = configuration["NEXT_PUBLIC_APP_URL"]
                     ?? configuration["APP_PUBLIC_URL"]
                     ?? "http://localhost:3000";
        return appUrl.TrimEnd('/');
    }

    /// <summary>
    /// Full absolute URL to the school logo image served by the web app.
    /// </summary>
    public static string ResolveLogoUrl(IConfiguration configuration)
    {
        return ResolveAppUrl(configuration) + LogoPath;
    }
}
