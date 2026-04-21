using MediatR;

namespace EduConnect.Api.Features.Exams.FinalizeExamResults;

/// <summary>
/// Locks the results for an exam and fans out per-student result
/// notifications: one in-app row + web push + email per parent, personalized
/// to each student's report card.
/// </summary>
public record FinalizeExamResultsCommand(Guid ExamId) : IRequest<FinalizeExamResultsResponse>;

public record FinalizeExamResultsResponse(
    string Message,
    int StudentCount,
    int NotifiedParentCount);
