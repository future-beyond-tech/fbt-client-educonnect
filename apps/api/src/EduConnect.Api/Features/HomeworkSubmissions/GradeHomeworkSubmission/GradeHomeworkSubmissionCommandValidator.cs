using FluentValidation;

namespace EduConnect.Api.Features.HomeworkSubmissions.GradeHomeworkSubmission;

public class GradeHomeworkSubmissionCommandValidator : AbstractValidator<GradeHomeworkSubmissionCommand>
{
    public GradeHomeworkSubmissionCommandValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.Grade)
            .NotEmpty().WithMessage("Grade is required.")
            .MaximumLength(32);
        RuleFor(x => x.Feedback)
            .MaximumLength(2000)
            .When(x => x.Feedback != null);
    }
}
