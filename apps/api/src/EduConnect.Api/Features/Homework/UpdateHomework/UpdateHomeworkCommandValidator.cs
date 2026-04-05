using FluentValidation;

namespace EduConnect.Api.Features.Homework.UpdateHomework;

public class UpdateHomeworkCommandValidator : AbstractValidator<UpdateHomeworkCommand>
{
    public UpdateHomeworkCommandValidator()
    {
        RuleFor(x => x.HomeworkId)
            .NotEmpty().WithMessage("Homework ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Due date must be in the future or today.");
    }
}
