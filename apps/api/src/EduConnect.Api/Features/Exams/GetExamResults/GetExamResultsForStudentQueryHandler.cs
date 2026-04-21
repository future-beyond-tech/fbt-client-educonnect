using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.GetExamResults;

public class GetExamResultsForStudentQueryHandler
    : IRequestHandler<GetExamResultsForStudentQuery, ExamResultStudentDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetExamResultsForStudentQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ExamResultStudentDto> Handle(
        GetExamResultsForStudentQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUserService.SchoolId;

        var exam = await _context.Exams
            .Include(e => e.Class)
            .Include(e => e.Subjects.OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime))
            .FirstOrDefaultAsync(e => e.Id == request.ExamId && !e.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exam", request.ExamId.ToString());

        var student = await _context.Students
            .FirstOrDefaultAsync(s =>
                s.Id == request.StudentId &&
                s.SchoolId == schoolId &&
                s.ClassId == exam.ClassId,
                cancellationToken)
            ?? throw new NotFoundException("Student", request.StudentId.ToString());

        // Visibility rules:
        //  - Parent: must be linked to this student AND results must be finalized.
        //  - Teacher: must be on the class (class teacher sees drafts too,
        //    regular assignee only sees finalized).
        //  - Admin: always sees.
        if (_currentUserService.Role == "Parent")
        {
            var linked = await _context.ParentStudentLinks
                .AnyAsync(psl =>
                    psl.SchoolId == schoolId &&
                    psl.ParentId == _currentUserService.UserId &&
                    psl.StudentId == student.Id,
                    cancellationToken);

            if (!linked || !exam.IsResultsFinalized)
            {
                throw new NotFoundException("Exam results", request.ExamId.ToString());
            }
        }
        else if (_currentUserService.Role == "Teacher")
        {
            var assignment = await _context.TeacherClassAssignments
                .Where(tca =>
                    tca.SchoolId == schoolId &&
                    tca.TeacherId == _currentUserService.UserId &&
                    tca.ClassId == exam.ClassId)
                .FirstOrDefaultAsync(cancellationToken);

            if (assignment is null)
            {
                throw new ForbiddenException("You are not assigned to this class.");
            }

            if (!assignment.IsClassTeacher && !exam.IsResultsFinalized)
            {
                throw new NotFoundException("Exam results", request.ExamId.ToString());
            }
        }
        else if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("You are not allowed to view exam results.");
        }

        var results = await _context.ExamResults
            .Where(r =>
                r.SchoolId == schoolId &&
                r.ExamId == exam.Id &&
                r.StudentId == student.Id)
            .ToListAsync(cancellationToken);
        var resultsBySubject = results.ToDictionary(r => r.ExamSubjectId);

        var lines = exam.Subjects
            .Select(s =>
            {
                if (resultsBySubject.TryGetValue(s.Id, out var r))
                {
                    return new ExamResultStudentLine(
                        s.Id, s.Subject, s.MaxMarks,
                        r.MarksObtained, r.Grade, r.Remarks, r.IsAbsent);
                }
                return new ExamResultStudentLine(
                    s.Id, s.Subject, s.MaxMarks,
                    null, null, null, false);
            })
            .ToList();

        var totalObtained = lines
            .Where(l => !l.IsAbsent && l.MarksObtained.HasValue)
            .Sum(l => l.MarksObtained!.Value);
        var totalMax = lines.Sum(l => l.MaxMarks);
        var percentage = totalMax > 0 ? (double)(totalObtained / totalMax) * 100.0 : 0.0;

        return new ExamResultStudentDto(
            exam.Id,
            exam.Name,
            exam.Class?.Name ?? string.Empty,
            exam.Class?.Section ?? string.Empty,
            student.Name,
            student.RollNumber,
            exam.IsResultsFinalized,
            exam.ResultsFinalizedAt,
            totalObtained,
            totalMax,
            percentage,
            lines);
    }
}
