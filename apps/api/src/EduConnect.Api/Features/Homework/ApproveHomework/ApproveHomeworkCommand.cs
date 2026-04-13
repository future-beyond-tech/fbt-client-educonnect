using MediatR;

namespace EduConnect.Api.Features.Homework.ApproveHomework;

public record ApproveHomeworkCommand(Guid HomeworkId) : IRequest<ApproveHomeworkResponse>;

public record ApproveHomeworkResponse(string Message);

