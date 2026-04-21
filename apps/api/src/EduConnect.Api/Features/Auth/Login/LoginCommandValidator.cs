using EduConnect.Api.Common.PhoneNumbers;
using FluentValidation;

namespace EduConnect.Api.Features.Auth.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(JapanPhoneNumber.IsValidInput).WithMessage(JapanPhoneNumber.ValidationMessage);

        // Login only enforces non-empty. Length/strength checks live in
        // PasswordPolicyValidator and are applied when passwords are SET, not
        // verified — users with pre-policy passwords must still be able to
        // sign in so the legacy-rotation flow can force them to change.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
