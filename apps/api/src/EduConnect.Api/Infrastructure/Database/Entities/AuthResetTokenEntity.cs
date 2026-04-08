namespace EduConnect.Api.Infrastructure.Database.Entities;

public class AuthResetTokenEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserEntity? User { get; set; }
}

public static class AuthResetTokenPurpose
{
    public const string Password = "Password";
    public const string Pin = "Pin";
}
