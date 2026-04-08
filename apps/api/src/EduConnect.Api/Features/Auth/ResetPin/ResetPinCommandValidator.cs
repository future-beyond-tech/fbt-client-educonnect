using FluentValidation;

namespace EduConnect.Api.Features.Auth.ResetPin;

public class ResetPinCommandValidator : AbstractValidator<ResetPinCommand>
{
    public ResetPinCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.")
            .MaximumLength(256).WithMessage("Reset token is invalid.");

        RuleFor(x => x.NewPin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be 4-6 digits.");

        RuleFor(x => x.ConfirmPin)
            .NotEmpty().WithMessage("Confirm PIN is required.")
            .Equal(x => x.NewPin).WithMessage("PINs do not match.");
    }
}
