using FluentValidation;

namespace EduConnect.Api.Features.Teachers.PromoteClassTeacher;

public class PromoteClassTeacherCommandValidator : AbstractValidator<PromoteClassTeacherCommand>
{
    public PromoteClassTeacherCommandValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Teacher ID is required.");

        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment ID is required.");
    }
}
