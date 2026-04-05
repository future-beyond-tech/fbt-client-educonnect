using FluentValidation;

namespace EduConnect.Api.Features.Students.UnlinkParentFromStudent;

public class UnlinkParentFromStudentCommandValidator : AbstractValidator<UnlinkParentFromStudentCommand>
{
    public UnlinkParentFromStudentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student ID is required.");

        RuleFor(x => x.LinkId)
            .NotEmpty().WithMessage("Link ID is required.");
    }
}
