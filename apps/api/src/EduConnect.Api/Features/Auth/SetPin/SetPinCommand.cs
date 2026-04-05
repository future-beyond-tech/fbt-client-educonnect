namespace EduConnect.Api.Features.Auth.SetPin;

public record SetPinCommand(string Pin, string ConfirmPin) : IRequest<Unit>;
