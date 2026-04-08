namespace EduConnect.Api.Features.Auth.ResetPassword;

public record ResetPasswordCommand(string Token, string NewPassword, string ConfirmPassword) : IRequest<Unit>;
