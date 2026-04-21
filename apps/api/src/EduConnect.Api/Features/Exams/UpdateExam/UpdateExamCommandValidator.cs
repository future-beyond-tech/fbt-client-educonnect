using EduConnect.Api.Features.Exams.CreateExam;
using FluentValidation;

namespace EduConnect.Api.Features.Exams.UpdateExam;

public class UpdateExamCommandValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamCommandValidator()
    {
        RuleFor(x => x.ExamId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Exam name is required.")
            .MaximumLength(128);

        RuleFor(x => x.AcademicYear)
            .NotEmpty().WithMessage("Academic year is required.")
            .MaximumLength(16);

        RuleFor(x => x.Subjects)
            .NotNull()
            .Must(s => s != null && s.Count > 0)
            .WithMessage("At least one subject is required.")
            .Must(subjects => subjects == null ||
                              subjects.Select(s => s.Subject.Trim().ToLowerInvariant()).Distinct().Count() == subjects.Count)
            .WithMessage("Subjects must be unique within an exam.");

        RuleForEach(x => x.Subjects).SetValidator(new CreateExamSubjectInputValidator());
    }
}
