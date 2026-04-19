namespace EduConnect.Api.Features.Homework.GetHomework;

public record GetHomeworkQuery(Guid? ClassId = null, string? Subject = null) : IRequest<List<HomeworkDto>>;

public record HomeworkDto(
    Guid HomeworkId,
    Guid ClassId,
    // ClassName / Section are denormalised from the Classes table so that
    // every consumer (teacher list, parent list, approvals view) can show the
    // class context alongside Subject/DueDate without a per-page lookup.
    // Kept nullable-string-with-default so the handler is free to project
    // them via a Left Join without throwing on orphaned rows.
    string ClassName,
    string Section,
    string Subject,
    string Title,
    string Description,
    DateOnly DueDate,
    bool IsEditable,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    Guid? ApprovedById,
    DateTimeOffset? RejectedAt,
    Guid? RejectedById,
    string? RejectedReason,
    bool CanSubmitForApproval,
    bool CanApproveOrReject,
    DateTimeOffset PublishedAt);
