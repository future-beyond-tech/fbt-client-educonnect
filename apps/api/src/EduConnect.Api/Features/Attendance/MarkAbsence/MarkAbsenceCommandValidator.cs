using FluentValidation;

namespace EduConnect.Api.Features.Attendance.MarkAbsence;

public class MarkAbsenceCommandValidator : AbstractValidator<MarkAbsenceCommand>
{
    public MarkAbsenceCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student ID is required.");

        RuleFor(x => x.Date)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Date cannot be in the future.")
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7))).WithMessage("Date cannot be more than 7 days ago.");
    }
}
