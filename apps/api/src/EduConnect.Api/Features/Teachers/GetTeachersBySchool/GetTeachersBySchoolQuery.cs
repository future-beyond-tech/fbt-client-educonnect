using EduConnect.Api.Common.Models;

namespace EduConnect.Api.Features.Teachers.GetTeachersBySchool;

public record GetTeachersBySchoolQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<TeacherListDto>>;

public record TeacherListDto(
    Guid Id,
    string Name,
    string Phone,
    bool IsActive,
    int AssignedClassCount,
    List<string> Subjects);
