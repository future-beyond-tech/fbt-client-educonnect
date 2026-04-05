namespace EduConnect.Api.Common.Auth;

public class CurrentUserService
{
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public bool IsAuthenticated => UserId != Guid.Empty && SchoolId != Guid.Empty;
}
