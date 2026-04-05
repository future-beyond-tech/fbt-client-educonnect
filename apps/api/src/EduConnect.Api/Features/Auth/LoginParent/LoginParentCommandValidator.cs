using FluentValidation;

namespace EduConnect.Api.Features.Auth.LoginParent;

public class LoginParentCommandValidator : AbstractValidator<LoginParentCommand>
{
    public LoginParentCommandValidator()
    {
        RuleFor(x => x.RollNumber)
            .NotEmpty().WithMessage("Roll number is required.")
            .MaximumLength(50).WithMessage("Roll number must not exceed 50 characters.");

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be 4-6 digits.");
    }
}
