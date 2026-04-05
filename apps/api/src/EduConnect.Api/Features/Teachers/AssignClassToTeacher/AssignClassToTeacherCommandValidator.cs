using FluentValidation;

namespace EduConnect.Api.Features.Teachers.AssignClassToTeacher;

public class AssignClassToTeacherCommandValidator : AbstractValidator<AssignClassToTeacherCommand>
{
    public AssignClassToTeacherCommandValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Teacher ID is required.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class ID is required.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(100).WithMessage("Subject cannot exceed 100 characters.");
    }
}
