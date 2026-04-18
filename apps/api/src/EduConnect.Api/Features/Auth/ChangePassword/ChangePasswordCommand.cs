namespace EduConnect.Api.Features.Auth.ChangePassword;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword, string ConfirmPassword) : IRequest<Unit>;
