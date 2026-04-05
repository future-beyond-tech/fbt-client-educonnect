using FluentValidation;

namespace EduConnect.Api.Features.Students.EnrollStudent;

public class EnrollStudentCommandValidator : AbstractValidator<EnrollStudentCommand>
{
    public EnrollStudentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Student name is required.")
            .MaximumLength(120).WithMessage("Student name cannot exceed 120 characters.");

        RuleFor(x => x.RollNumber)
            .NotEmpty().WithMessage("Roll number is required.")
            .MaximumLength(20).WithMessage("Roll number cannot exceed 20 characters.")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("Roll number can only contain letters, numbers, and hyphens.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class ID is required.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Date of birth cannot be in the future.");
    }
}
