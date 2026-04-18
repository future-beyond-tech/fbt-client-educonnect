namespace EduConnect.Api.Infrastructure.Database.Entities;

public class UserEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? PinHash { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SchoolEntity? School { get; set; }
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = [];
}
