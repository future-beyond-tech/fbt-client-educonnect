using MediatR;

namespace EduConnect.Api.Features.Homework.RejectHomework;

public record RejectHomeworkCommand(Guid HomeworkId, string Reason) : IRequest<RejectHomeworkResponse>;

public record RejectHomeworkResponse(string Message);

