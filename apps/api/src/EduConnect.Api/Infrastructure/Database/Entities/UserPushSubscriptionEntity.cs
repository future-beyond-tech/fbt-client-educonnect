namespace EduConnect.Api.Infrastructure.Database.Entities;

public class UserPushSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Push service endpoint URL provided by the browser
    /// (unique per device + browser profile).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// P-256 ECDH public key (base64url) from the browser subscription.
    /// </summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>
    /// HMAC auth secret (base64url) from the browser subscription.
    /// </summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent captured at subscription time. Helps users recognize devices
    /// in a future "manage devices" screen.
    /// </summary>
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }

    public SchoolEntity? School { get; set; }
    public UserEntity? User { get; set; }
}
