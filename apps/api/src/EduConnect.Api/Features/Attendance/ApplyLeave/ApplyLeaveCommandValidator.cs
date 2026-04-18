using FluentValidation;

namespace EduConnect.Api.Features.Attendance.ApplyLeave;

public class ApplyLeaveCommandValidator : AbstractValidator<ApplyLeaveCommand>
{
    // Hard cap on the number of children a single leave submission can cover.
    // Parents rarely have more than a handful of children at one school; this
    // guards against a client accidentally (or maliciously) sending a huge
    // payload that would balloon the DB round trip / notification fan-out.
    public const int MaxStudentsPerRequest = 20;

    public ApplyLeaveCommandValidator()
    {
        // Accept either StudentIds (preferred) or the legacy StudentId field.
        RuleFor(x => x)
            .Must(HasAtLeastOneStudent)
            .WithName("StudentIds")
            .WithMessage("Select at least one child to apply leave for.");

        When(x => x.StudentIds is { Length: > 0 }, () =>
        {
            RuleFor(x => x.StudentIds)
                .Must(ids => ids.Length <= MaxStudentsPerRequest)
                .WithMessage($"You can apply leave for at most {MaxStudentsPerRequest} children in one request.");

            RuleForEach(x => x.StudentIds)
                .NotEmpty().WithMessage("Student ID is required.");

            RuleFor(x => x.StudentIds)
                .Must(ids => ids.Distinct().Count() == ids.Length)
                .WithMessage("Duplicate children are not allowed in the same leave request.");
        });

        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Start date must be today or in the future.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after the start date.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(1000).WithMessage("Reason must not exceed 1000 characters.");
    }

    private static bool HasAtLeastOneStudent(ApplyLeaveCommand cmd) =>
        (cmd.StudentIds is { Length: > 0 })
        || (cmd.StudentId is Guid id && id != Guid.Empty);
}
