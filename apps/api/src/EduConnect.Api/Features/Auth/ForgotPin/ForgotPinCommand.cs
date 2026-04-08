namespace EduConnect.Api.Features.Auth.ForgotPin;

public record ForgotPinCommand(string Email) : IRequest<Unit>;
