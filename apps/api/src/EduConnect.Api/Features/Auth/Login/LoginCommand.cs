namespace EduConnect.Api.Features.Auth.Login;

public record LoginCommand(string Phone, string Password) : IRequest<LoginResponse>;

public record LoginResponse(string AccessToken, int ExpiresIn, bool MustChangePassword);
