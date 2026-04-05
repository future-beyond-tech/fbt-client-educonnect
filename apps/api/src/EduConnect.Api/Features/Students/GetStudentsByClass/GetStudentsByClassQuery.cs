using EduConnect.Api.Common.Models;

namespace EduConnect.Api.Features.Students.GetStudentsByClass;

public record GetStudentsByClassQuery(
    Guid? ClassId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StudentListDto>>;

public record StudentListDto(
    Guid Id,
    string Name,
    string RollNumber,
    Guid ClassId,
    string ClassName,
    string Section,
    bool IsActive,
    DateOnly? DateOfBirth);
