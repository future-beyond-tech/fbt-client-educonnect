using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.GetExamById;

public class GetExamByIdQueryHandler : IRequestHandler<GetExamByIdQuery, ExamDetailDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetExamByIdQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ExamDetailDto> Handle(GetExamByIdQuery request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUserService.SchoolId;
        var userId = _currentUserService.UserId;
        var role = _currentUserService.Role;

        var exam = await _context.Exams
            .Include(e => e.Class)
            .Include(e => e.Subjects.OrderBy(s => s.ExamDate).ThenBy(s => s.StartTime))
            .FirstOrDefaultAsync(e => e.Id == request.ExamId && !e.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exam", request.ExamId.ToString());

        // Parents may only view published schedules for their children's classes.
        if (role == "Parent")
        {
            var parentClassIds = await _context.ParentStudentLinks
                .Where(psl => psl.SchoolId == schoolId && psl.ParentId == userId)
                .Join(_context.Students, psl => psl.StudentId, s => s.Id, (psl, s) => s.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!exam.IsSchedulePublished || !parentClassIds.Contains(exam.ClassId))
            {
                throw new NotFoundException("Exam", request.ExamId.ToString());
            }
        }
        else if (role == "Teacher")
        {
            var canSee = await _context.TeacherClassAssignments
                .AnyAsync(tca =>
                    tca.SchoolId == schoolId &&
                    tca.TeacherId == userId &&
                    tca.ClassId == exam.ClassId, cancellationToken);
            if (!canSee)
            {
                throw new NotFoundException("Exam", request.ExamId.ToString());
            }
        }

        var isClassTeacher = role == "Teacher" && await _context.TeacherClassAssignments
            .AnyAsync(tca =>
                tca.SchoolId == schoolId &&
                tca.TeacherId == userId &&
                tca.ClassId == exam.ClassId &&
                tca.IsClassTeacher, cancellationToken);

        var canEditSchedule = isClassTeacher && !exam.IsSchedulePublished;
        var canEditResults = isClassTeacher && !exam.IsResultsFinalized;

        return new ExamDetailDto(
            exam.Id,
            exam.ClassId,
            exam.Class?.Name ?? string.Empty,
            exam.Class?.Section ?? string.Empty,
            exam.Name,
            exam.AcademicYear,
            exam.IsSchedulePublished,
            exam.SchedulePublishedAt,
            exam.IsResultsFinalized,
            exam.ResultsFinalizedAt,
            canEditSchedule,
            canEditResults,
            exam.Subjects
                .Select(s => new ExamSubjectDto(
                    s.Id,
                    s.Subject,
                    s.ExamDate,
                    s.StartTime,
                    s.EndTime,
                    s.MaxMarks,
                    s.Room))
                .ToList());
    }
}
