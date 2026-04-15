using FluentValidation;

namespace EduConnect.Api.Features.Students.EnrollStudent;

public class EnrollStudentCommandValidator : AbstractValidator<EnrollStudentCommand>
{
    private static readonly string[] AllowedRelationships =
        { "parent", "guardian", "grandparent", "sibling", "other" };

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

        When(x => x.Parent is not null, () =>
        {
            RuleFor(x => x.Parent!)
                .SetValidator(new EnrollStudentParentRequestValidator());
        });
    }

    private sealed class EnrollStudentParentRequestValidator : AbstractValidator<EnrollStudentParentRequest>
    {
        public EnrollStudentParentRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Parent name is required.")
                .MaximumLength(200).WithMessage("Parent name cannot exceed 200 characters.");

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^\d{10}$").WithMessage("Phone number must be exactly 10 digits.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Enter a valid email address.")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

            RuleFor(x => x.Pin)
                .NotEmpty().WithMessage("PIN is required.")
                .Matches(@"^\d{4,6}$").WithMessage("PIN must be 4-6 digits.");

            RuleFor(x => x.Relationship)
                .NotEmpty().WithMessage("Relationship is required.")
                .MaximumLength(30).WithMessage("Relationship cannot exceed 30 characters.")
                .Must(r => !string.IsNullOrWhiteSpace(r) &&
                           AllowedRelationships.Contains(r.ToLowerInvariant()))
                .WithMessage($"Relationship must be one of: {string.Join(", ", AllowedRelationships)}.");
        }
    }
}
