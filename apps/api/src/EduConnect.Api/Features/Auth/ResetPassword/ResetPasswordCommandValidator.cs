using EduConnect.Api.Common.Auth;
using FluentValidation;

namespace EduConnect.Api.Features.Auth.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.")
            .MaximumLength(256).WithMessage("Reset token is invalid.");

        RuleFor(x => x.NewPassword)
            .SetValidator(new PasswordPolicyValidator());

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
