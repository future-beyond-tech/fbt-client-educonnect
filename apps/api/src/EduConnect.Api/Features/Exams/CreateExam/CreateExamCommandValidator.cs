using FluentValidation;

namespace EduConnect.Api.Features.Exams.CreateExam;

public class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Exam name is required.")
            .MaximumLength(128).WithMessage("Exam name cannot exceed 128 characters.");

        RuleFor(x => x.AcademicYear)
            .NotEmpty().WithMessage("Academic year is required.")
            .MaximumLength(16).WithMessage("Academic year cannot exceed 16 characters.");

        RuleFor(x => x.Subjects)
            .NotNull().WithMessage("At least one subject is required.")
            .Must(subjects => subjects != null && subjects.Count > 0)
            .WithMessage("At least one subject is required.")
            .Must(subjects => subjects == null ||
                              subjects.Select(s => s.Subject.Trim().ToLowerInvariant()).Distinct().Count() == subjects.Count)
            .WithMessage("Subjects must be unique within an exam.");

        RuleForEach(x => x.Subjects).SetValidator(new CreateExamSubjectInputValidator());
    }
}

public class CreateExamSubjectInputValidator : AbstractValidator<CreateExamSubjectInput>
{
    public CreateExamSubjectInputValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(100).WithMessage("Subject cannot exceed 100 characters.");

        RuleFor(x => x.MaxMarks)
            .GreaterThan(0m).WithMessage("Max marks must be greater than 0.")
            .LessThanOrEqualTo(9999.99m).WithMessage("Max marks must fit in numeric(6,2).");

        RuleFor(x => x)
            .Must(x => x.EndTime > x.StartTime)
            .WithMessage("End time must be after start time.");

        RuleFor(x => x.ExamDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
            .WithMessage("Exam date must be today or in the future.");

        RuleFor(x => x.Room)
            .MaximumLength(64).WithMessage("Room cannot exceed 64 characters.");
    }
}
