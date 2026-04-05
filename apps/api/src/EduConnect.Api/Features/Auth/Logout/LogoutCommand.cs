namespace EduConnect.Api.Features.Auth.Logout;

public record LogoutCommand(Guid UserId) : IRequest<Unit>;
