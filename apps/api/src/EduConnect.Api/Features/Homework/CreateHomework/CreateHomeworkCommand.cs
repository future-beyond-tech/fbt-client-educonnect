namespace EduConnect.Api.Features.Homework.CreateHomework;

public record CreateHomeworkCommand(
    Guid ClassId,
    string Subject,
    string Title,
    string Description,
    DateOnly DueDate) : IRequest<CreateHomeworkResponse>;

public record CreateHomeworkResponse(Guid HomeworkId, string Message);
