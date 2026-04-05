using FluentValidation;

namespace EduConnect.Api.Features.Students.LinkParentToStudent;

public class LinkParentToStudentCommandValidator : AbstractValidator<LinkParentToStudentCommand>
{
    private static readonly string[] AllowedRelationships =
        { "parent", "guardian", "grandparent", "sibling", "other" };

    public LinkParentToStudentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student ID is required.");

        RuleFor(x => x.ParentId)
            .NotEmpty().WithMessage("Parent ID is required.");

        RuleFor(x => x.Relationship)
            .NotEmpty().WithMessage("Relationship is required.")
            .MaximumLength(30).WithMessage("Relationship cannot exceed 30 characters.")
            .Must(r => AllowedRelationships.Contains(r.ToLower()))
            .WithMessage($"Relationship must be one of: {string.Join(", ", AllowedRelationships)}.");
    }
}
