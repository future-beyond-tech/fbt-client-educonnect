using FluentValidation;

namespace EduConnect.Api.Features.Exams.UpsertExamResults;

public class UpsertExamResultsCommandValidator : AbstractValidator<UpsertExamResultsCommand>
{
    public UpsertExamResultsCommandValidator()
    {
        RuleFor(x => x.ExamId).NotEmpty();
        RuleFor(x => x.Rows)
            .NotNull()
            .Must(r => r != null && r.Count > 0)
            .WithMessage("At least one result row is required.");

        RuleForEach(x => x.Rows).SetValidator(new ExamResultRowInputValidator());
    }
}

public class ExamResultRowInputValidator : AbstractValidator<ExamResultRowInput>
{
    public ExamResultRowInputValidator()
    {
        RuleFor(r => r.StudentId).NotEmpty();
        RuleFor(r => r.ExamSubjectId).NotEmpty();

        // Absent students must not carry a mark.
        RuleFor(r => r)
            .Must(r => !(r.IsAbsent && r.MarksObtained.HasValue))
            .WithMessage("An absent student cannot have marks recorded.");

        // At least one of marks/grade/absent must be supplied, else the row
        // carries no signal. This mirrors the DB check constraint.
        RuleFor(r => r)
            .Must(r => r.IsAbsent || r.MarksObtained.HasValue || !string.IsNullOrWhiteSpace(r.Grade))
            .WithMessage("Each result must have marks, a grade, or be marked absent.");

        RuleFor(r => r.MarksObtained)
            .GreaterThanOrEqualTo(0)
            .When(r => r.MarksObtained.HasValue);

        RuleFor(r => r.Grade).MaximumLength(8);
        RuleFor(r => r.Remarks).MaximumLength(500);
    }
}
