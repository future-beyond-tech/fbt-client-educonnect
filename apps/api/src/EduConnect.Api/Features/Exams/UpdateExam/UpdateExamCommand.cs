using EduConnect.Api.Features.Exams.CreateExam;
using MediatR;

namespace EduConnect.Api.Features.Exams.UpdateExam;

public record UpdateExamCommand(
    Guid ExamId,
    string Name,
    string AcademicYear,
    IReadOnlyList<CreateExamSubjectInput> Subjects) : IRequest<UpdateExamResponse>;

public record UpdateExamResponse(string Message);
