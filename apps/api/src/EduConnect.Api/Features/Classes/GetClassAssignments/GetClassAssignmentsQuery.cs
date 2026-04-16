using EduConnect.Api.Common.Models;

namespace EduConnect.Api.Features.Classes.GetClassAssignments;

public record GetClassAssignmentsQuery(Guid ClassId) : IRequest<List<ClassAssignmentDto>>;

public record ClassAssignmentDto(
    Guid AssignmentId,
    Guid TeacherId,
    string TeacherName,
    string TeacherPhone,
    string Subject,
    bool IsClassTeacher,
    DateTimeOffset AssignedAt);

