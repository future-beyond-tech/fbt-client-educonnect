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

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}
