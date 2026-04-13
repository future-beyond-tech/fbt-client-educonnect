using MediatR;

namespace EduConnect.Api.Features.Homework.SubmitHomeworkForApproval;

public record SubmitHomeworkForApprovalCommand(Guid HomeworkId) : IRequest<SubmitHomeworkForApprovalResponse>;

public record SubmitHomeworkForApprovalResponse(string Message);

