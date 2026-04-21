using FluentValidation;

namespace EduConnect.Api.Features.HomeworkSubmissions.SubmitHomework;

public class SubmitHomeworkCommandValidator : AbstractValidator<SubmitHomeworkCommand>
{
    public SubmitHomeworkCommandValidator()
    {
        RuleFor(x => x.HomeworkId).NotEmpty().WithMessage("Homework ID is required.");
        RuleFor(x => x.StudentId).NotEmpty().WithMessage("Student ID is required.");

        RuleFor(x => x.BodyText)
            .MaximumLength(4000)
            .When(x => x.BodyText != null);

        // A submission must have either body text or at least one attachment.
        RuleFor(x => x)
            .Must(c => !string.IsNullOrWhiteSpace(c.BodyText)
                       || (c.AttachmentIds != null && c.AttachmentIds.Count > 0))
            .WithMessage("Submission must include either text or at least one attachment.");
    }
}
