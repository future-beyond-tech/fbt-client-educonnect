using FluentValidation;

namespace EduConnect.Api.Features.Notices.CreateNotice;

public class CreateNoticeCommandValidator : AbstractValidator<CreateNoticeCommand>
{
    public CreateNoticeCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required.")
            .MaximumLength(5000).WithMessage("Body cannot exceed 5000 characters.");

        RuleFor(x => x.TargetAudience)
            .NotEmpty().WithMessage("Target audience is required.")
            .Must(x => x == "All" || x == "Class" || x == "Section")
            .WithMessage("Target audience must be 'All', 'Class', or 'Section'.");

        RuleFor(x => x.TargetClassId)
            .NotEmpty().WithMessage("Target class ID is required when targeting a specific class or section.")
            .When(x => x.TargetAudience == "Class" || x.TargetAudience == "Section");
    }
}
