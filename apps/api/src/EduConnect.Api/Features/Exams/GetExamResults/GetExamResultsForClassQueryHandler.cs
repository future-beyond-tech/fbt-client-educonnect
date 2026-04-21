using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.GetExamResults;

public class GetExamResultsForClassQueryHandler
    : IRequestHandler<GetExamResultsForClassQuery, ExamResultsGridDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetExamResultsForClassQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ExamResultsGridDto> Handle(
        GetExamResultsForClassQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Teacher" && _currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only teachers or admins can view the class results grid.");
        }

        var exam = await _context.Exams
            .Include(e => e.Class)
            .Include(e => e.Subjects.OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime))
            .FirstOrDefaultAsync(e => e.Id == request.ExamId && !e.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exam", request.ExamId.ToString());

        // Teacher must be on the class (class teacher OR regular assignment).
        var isClassTeacher = false;
        if (_currentUserService.Role == "Teacher")
        {
            var assignment = await _context.TeacherClassAssignments
                .Where(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId &&
                    tca.ClassId == exam.ClassId)
                .FirstOrDefaultAsync(cancellationToken);

            if (assignment is null)
            {
                throw new ForbiddenException("You are not assigned to this class.");
            }

            isClassTeacher = assignment.IsClassTeacher;
        }

        var subjectColumns = exam.Subjects
            .Select(s => new ExamResultsSubjectColumn(s.Id, s.Subject, s.MaxMarks))
            .ToList();

        var students = await _context.Students
            .Where(s => s.SchoolId == _currentUserService.SchoolId &&
                        s.ClassId == exam.ClassId &&
                        s.IsActive)
            .OrderBy(s => s.RollNumber)
            .Select(s => new { s.Id, s.RollNumber, s.Name })
            .ToListAsync(cancellationToken);

        var results = await _context.ExamResults
            .Where(r => r.SchoolId == _currentUserService.SchoolId && r.ExamId == exam.Id)
            .ToListAsync(cancellationToken);
        var resultsByKey = results.ToDictionary(r => (r.ExamSubjectId, r.StudentId));

        var studentRows = students
            .Select(s => new ExamResultsStudentRow(
                s.Id,
                s.RollNumber,
                s.Name,
                subjectColumns
                    .Select(col =>
                    {
                        if (resultsByKey.TryGetValue((col.ExamSubjectId, s.Id), out var r))
                        {
                            return new ExamResultsCell(
                                col.ExamSubjectId,
                                r.MarksObtained,
                                r.Grade,
                                r.Remarks,
                                r.IsAbsent);
                        }

                        return new ExamResultsCell(col.ExamSubjectId, null, null, null, false);
                    })
                    .ToList()))
            .ToList();

        var canEdit = isClassTeacher && exam.IsSchedulePublished && !exam.IsResultsFinalized;

        return new ExamResultsGridDto(
            exam.Id,
            exam.Name,
            exam.Class?.Name ?? string.Empty,
            exam.Class?.Section ?? string.Empty,
            exam.IsResultsFinalized,
            exam.ResultsFinalizedAt,
            canEdit,
            subjectColumns,
            studentRows);
    }
}
