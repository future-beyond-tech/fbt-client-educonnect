using FluentValidation;

namespace EduConnect.Api.Features.Auth.ForgotPin;

public class ForgotPinCommandValidator : AbstractValidator<ForgotPinCommand>
{
    public ForgotPinCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.");
    }
}
