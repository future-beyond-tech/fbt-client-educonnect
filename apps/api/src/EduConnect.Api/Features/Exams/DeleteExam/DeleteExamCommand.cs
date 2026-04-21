using MediatR;

namespace EduConnect.Api.Features.Exams.DeleteExam;

public record DeleteExamCommand(Guid ExamId) : IRequest<DeleteExamResponse>;

public record DeleteExamResponse(string Message);
