using FluentValidation;

namespace EduConnect.Api.Features.Students.GetStudentsByClass;

public class GetStudentsByClassQueryValidator : AbstractValidator<GetStudentsByClassQuery>
{
    private static readonly string[] AllowedStatuses = ["active", "inactive"];
    private static readonly string[] AllowedSortBy = ["nameAsc", "nameDesc", "rollAsc", "createdDesc"];

    public GetStudentsByClassQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(v => string.IsNullOrWhiteSpace(v) ||
                       AllowedStatuses.Contains(v.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: active, inactive.");

        RuleFor(x => x.SortBy)
            .Must(v => string.IsNullOrWhiteSpace(v) ||
                       AllowedSortBy.Contains(v.Trim(), StringComparer.Ordinal))
            .WithMessage("SortBy must be one of: nameAsc, nameDesc, rollAsc, createdDesc.");

        RuleFor(x => x.ClassIds)
            .MaximumLength(2000).WithMessage("ClassIds filter is too long.")
            .When(x => x.ClassIds is not null);
    }
}
