namespace EduConnect.Api.Features.Auth.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<Unit>;
