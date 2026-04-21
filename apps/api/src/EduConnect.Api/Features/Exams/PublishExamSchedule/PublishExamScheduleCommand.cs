using MediatR;

namespace EduConnect.Api.Features.Exams.PublishExamSchedule;

/// <summary>
/// Publishes a draft exam schedule. Flips IsSchedulePublished=true on the
/// exam (making the schedule immutable), then fans out in-app notifications,
/// web push, and emails to every parent of every student in the target class.
/// </summary>
public record PublishExamScheduleCommand(Guid ExamId) : IRequest<PublishExamScheduleResponse>;

public record PublishExamScheduleResponse(string Message, int NotifiedParentCount);
