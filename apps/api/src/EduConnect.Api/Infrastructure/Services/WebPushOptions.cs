namespace EduConnect.Api.Infrastructure.Services;

public class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>Subject claim for VAPID (mailto: or https://) — identifies the sender to push services.</summary>
    public string Subject { get; set; } = "mailto:support@educonnect.app";

    /// <summary>Base64url-encoded VAPID public key.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Base64url-encoded VAPID private key (secret; load from env var).</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>If false, WebPushSender becomes a no-op. Use to disable push without removing wiring.</summary>
    public bool Enabled { get; set; } = true;
}
