namespace EduConnect.Api.Features.Homework.UpdateHomework;

public record UpdateHomeworkCommand(
    Guid HomeworkId,
    string Title,
    string Description,
    DateOnly DueDate) : IRequest<UpdateHomeworkResponse>;

public record UpdateHomeworkResponse(string Message);
