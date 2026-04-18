namespace EduConnect.Api.Features.Auth.ChangePin;

public record ChangePinCommand(string CurrentPin, string NewPin, string ConfirmPin) : IRequest<Unit>;
