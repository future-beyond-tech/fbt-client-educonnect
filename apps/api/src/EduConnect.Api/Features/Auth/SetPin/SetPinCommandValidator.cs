using FluentValidation;

namespace EduConnect.Api.Features.Auth.SetPin;

public class SetPinCommandValidator : AbstractValidator<SetPinCommand>
{
    public SetPinCommandValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be 4-6 digits.");

        RuleFor(x => x.ConfirmPin)
            .NotEmpty().WithMessage("Confirm PIN is required.")
            .Equal(x => x.Pin).WithMessage("PINs do not match.");
    }
}
