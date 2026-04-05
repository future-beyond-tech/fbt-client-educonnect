namespace EduConnect.Api.Features.Homework.GetHomework;

public record GetHomeworkQuery(Guid? ClassId = null, string? Subject = null) : IRequest<List<HomeworkDto>>;

public record HomeworkDto(
    Guid HomeworkId,
    Guid ClassId,
    string Subject,
    string Title,
    string Description,
    DateOnly DueDate,
    bool IsEditable,
    DateTimeOffset PublishedAt);
