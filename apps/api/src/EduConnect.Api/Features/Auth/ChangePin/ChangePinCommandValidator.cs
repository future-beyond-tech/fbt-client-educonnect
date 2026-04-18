using FluentValidation;

namespace EduConnect.Api.Features.Auth.ChangePin;

public class ChangePinCommandValidator : AbstractValidator<ChangePinCommand>
{
    public ChangePinCommandValidator()
    {
        RuleFor(x => x.CurrentPin)
            .NotEmpty().WithMessage("Current PIN is required.")
            .Matches("^[0-9]{4,6}$").WithMessage("Current PIN must be 4–6 digits.");

        RuleFor(x => x.NewPin)
            .NotEmpty().WithMessage("New PIN is required.")
            .Matches("^[0-9]{4,6}$").WithMessage("New PIN must be 4–6 digits.")
            .NotEqual(x => x.CurrentPin)
                .WithMessage("New PIN must be different from the current PIN.");

        RuleFor(x => x.ConfirmPin)
            .NotEmpty().WithMessage("Confirm PIN is required.")
            .Equal(x => x.NewPin).WithMessage("PINs do not match.");
    }
}
