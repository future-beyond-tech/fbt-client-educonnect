namespace EduConnect.Api.Features.Attendance.GetLeaveApplications;

public record GetLeaveApplicationsQuery(
    Guid? StudentId,
    string? Status,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<GetLeaveApplicationsResponse>;

public record LeaveApplicationDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string RollNumber,
    string ClassName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    string Status,
    string? ReviewNote,
    DateTimeOffset CreatedAt
);

public record GetLeaveApplicationsResponse(
    List<LeaveApplicationDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
