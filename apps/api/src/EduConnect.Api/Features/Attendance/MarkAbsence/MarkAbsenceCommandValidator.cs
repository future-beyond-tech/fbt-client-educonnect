using FluentValidation;

namespace EduConnect.Api.Features.Attendance.MarkAbsence;

public class MarkAbsenceCommandValidator : AbstractValidator<MarkAbsenceCommand>
{
    public MarkAbsenceCommandValidator()
    {
        RuleFor(x => x.RollNumber)
            .NotEmpty().WithMessage("Roll number is required.")
            .MaximumLength(50).WithMessage("Roll number must not exceed 50 characters.");

        RuleFor(x => x.Date)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date cannot be in the future.")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7))).WithMessage("Date cannot be more than 7 days ago.");
    }
}
