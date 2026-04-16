using EduConnect.Api.Common.PhoneNumbers;
using FluentValidation;

namespace EduConnect.Api.Features.Teachers.CreateTeacher;

public class CreateTeacherCommandValidator : AbstractValidator<CreateTeacherCommand>
{
    private static readonly string[] AllowedRoles = ["Teacher", "Admin"];

    public CreateTeacherCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(JapanPhoneNumber.IsValidInput).WithMessage(JapanPhoneNumber.ValidationMessage);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must be 128 characters or fewer.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => AllowedRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be either Teacher or Admin.");

        When(x => IsTeacherRole(x.Role), () =>
        {
            RuleFor(x => x)
                .Must(x =>
                {
                    var hasClass = x.ClassId.HasValue;
                    var hasSubject = !string.IsNullOrWhiteSpace(x.Subject);
                    return hasClass == hasSubject;
                })
                .WithMessage("Provide both a class and a subject, or leave both blank.");

            RuleFor(x => x.Subject!)
                .NotEmpty().When(x => x.ClassId.HasValue)
                .MaximumLength(100).WithMessage("Subject cannot exceed 100 characters.");
        });

        When(x => IsAdminRole(x.Role), () =>
        {
            RuleFor(x => x)
                .Must(x => !x.ClassId.HasValue && string.IsNullOrWhiteSpace(x.Subject) && !x.IsClassTeacher)
                .WithMessage("Admin accounts cannot be created with class assignments.");
        });
    }

    private static bool IsTeacherRole(string role) =>
        string.Equals(role?.Trim(), "Teacher", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminRole(string role) =>
        string.Equals(role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);
}
