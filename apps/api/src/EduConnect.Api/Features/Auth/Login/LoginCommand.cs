namespace EduConnect.Api.Features.Auth.Login;

public record LoginCommand(string Phone, string Password) : IRequest<string>;
