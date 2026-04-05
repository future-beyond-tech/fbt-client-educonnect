using FluentValidation;

namespace EduConnect.Api.Features.Attendance.AdminOverride;

public class AdminOverrideCommandValidator : AbstractValidator<AdminOverrideCommand>
{
    public AdminOverrideCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty().WithMessage("Record ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}
