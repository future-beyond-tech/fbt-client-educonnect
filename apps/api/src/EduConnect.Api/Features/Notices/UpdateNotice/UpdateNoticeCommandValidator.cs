using FluentValidation;

namespace EduConnect.Api.Features.Notices.UpdateNotice;

public class UpdateNoticeCommandValidator : AbstractValidator<UpdateNoticeCommand>
{
    public UpdateNoticeCommandValidator()
    {
        RuleFor(x => x.NoticeId)
            .NotEmpty().WithMessage("Notice id is required.");

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

        RuleFor(x => x.TargetClassIds)
            .Must(ids => ids == null || ids.Count == ids.Distinct().Count())
            .WithMessage("Duplicate class targets are not allowed.");

        RuleFor(x => x.TargetClassIds)
            .Must(ids => ids == null || ids.Count == 0)
            .When(x => x.TargetAudience == "All")
            .WithMessage("Whole-school notices cannot include targeted classes.");

        RuleFor(x => x.TargetClassIds)
            .NotEmpty().WithMessage("Select at least one class section when targeting a class or section.")
            .When(x => x.TargetAudience == "Class" || x.TargetAudience == "Section");
    }
}
