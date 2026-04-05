using FluentValidation;

namespace EduConnect.Api.Features.Subjects.CreateSubject;

public class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Subject name is required.")
            .MaximumLength(80).WithMessage("Subject name cannot exceed 80 characters.");
    }
}
