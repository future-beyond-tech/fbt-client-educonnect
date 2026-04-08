namespace EduConnect.Api.Features.Auth.ResetPin;

public record ResetPinCommand(string Token, string NewPin, string ConfirmPin) : IRequest<Unit>;
