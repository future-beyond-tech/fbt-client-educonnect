using FluentValidation;

namespace EduConnect.Api.Features.Attendance.SubmitAttendanceTake;

public class SubmitAttendanceTakeCommandValidator : AbstractValidator<SubmitAttendanceTakeCommand>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Present",
        "Absent",
        "Late",
    };

    // How many days back a teacher is allowed to mark attendance for.
    // Anything older requires admin involvement (see bug report: attendance
    // could previously be marked for arbitrary past dates, enabling
    // record manipulation). Future dates are never allowed.
    public const int MaxBackdateDays = 7;

    public SubmitAttendanceTakeCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class ID is required.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Attendance cannot be marked for a future date.")
            .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-MaxBackdateDays))
            .WithMessage($"Attendance can only be marked for the last {MaxBackdateDays} days. Contact an admin to change older records.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items are required.")
            .Must(items => items.Count > 0).WithMessage("At least one attendance item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.StudentId)
                .NotEmpty().WithMessage("Student ID is required.");

            item.RuleFor(i => i.Status)
                .NotEmpty().WithMessage("Status is required.")
                .Must(status => AllowedStatuses.Contains(status)).WithMessage("Status must be Present, Absent, or Late.");

            item.RuleFor(i => i.Reason)
                .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
        });
    }
}

