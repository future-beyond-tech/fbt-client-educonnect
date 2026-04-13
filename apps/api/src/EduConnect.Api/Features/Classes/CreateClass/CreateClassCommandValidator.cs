using FluentValidation;

namespace EduConnect.Api.Features.Classes.CreateClass;

public class CreateClassCommandValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Class name is required.")
            .MaximumLength(100).WithMessage("Class name cannot exceed 100 characters.");

        RuleFor(x => x.Section)
            .NotEmpty().WithMessage("Section is required.")
            .MaximumLength(50).WithMessage("Section cannot exceed 50 characters.");

        RuleFor(x => x.AcademicYear)
            .NotEmpty().WithMessage("Academic year is required.")
            .MaximumLength(50).WithMessage("Academic year cannot exceed 50 characters.");
    }
}
