using EduConnect.Api.Common.Auth;
using FluentValidation;

namespace EduConnect.Api.Features.Auth.ChangePassword;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.")
            .MaximumLength(PasswordPolicy.MaxLength).WithMessage("Current password is invalid.");

        RuleFor(x => x.NewPassword)
            .SetValidator(new PasswordPolicyValidator())
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("New password must be different from the current password.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
