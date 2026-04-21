using FluentValidation;

namespace EduConnect.Api.Features.Teachers.GetTeachersBySchool;

public class GetTeachersBySchoolQueryValidator : AbstractValidator<GetTeachersBySchoolQuery>
{
    private static readonly string[] AllowedClassLoads = ["unassigned", "light", "heavy"];
    private static readonly string[] AllowedSortBy = ["nameAsc", "nameDesc", "classesDesc", "classesAsc", "createdDesc"];

    public GetTeachersBySchoolQueryValidator()
    {
        RuleFor(x => x.ClassLoad)
            .Must(v => string.IsNullOrWhiteSpace(v) ||
                       AllowedClassLoads.Contains(v.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("ClassLoad must be one of: unassigned, light, heavy.");

        RuleFor(x => x.SortBy)
            .Must(v => string.IsNullOrWhiteSpace(v) || AllowedSortBy.Contains(v.Trim(), StringComparer.Ordinal))
            .WithMessage("SortBy must be one of: nameAsc, nameDesc, classesDesc, classesAsc, createdDesc.");

        RuleFor(x => x.Subjects)
            .MaximumLength(1000).WithMessage("Subjects filter is too long.")
            .When(x => x.Subjects is not null);
    }
}
