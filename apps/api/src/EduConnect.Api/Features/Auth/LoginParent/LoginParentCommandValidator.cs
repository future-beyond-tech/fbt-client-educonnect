using EduConnect.Api.Common.PhoneNumbers;
using FluentValidation;

namespace EduConnect.Api.Features.Auth.LoginParent;

public class LoginParentCommandValidator : AbstractValidator<LoginParentCommand>
{
    public LoginParentCommandValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(JapanPhoneNumber.IsValidInput).WithMessage(JapanPhoneNumber.ValidationMessage);

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be 4-6 digits.");
    }
}
