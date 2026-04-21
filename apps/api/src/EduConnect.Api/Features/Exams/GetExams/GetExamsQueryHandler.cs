using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Exams.GetExams;

public class GetExamsQueryHandler : IRequestHandler<GetExamsQuery, List<ExamListItemDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetExamsQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ExamListItemDto>> Handle(GetExamsQuery request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUserService.SchoolId;
        var userId = _currentUserService.UserId;
        var role = _currentUserService.Role;

        var query = _context.Exams
            .Where(e => e.SchoolId == schoolId && !e.IsDeleted);

        if (role == "Parent")
        {
            // Parent sees only PUBLISHED schedules for classes their children attend.
            var studentClassIds = await _context.ParentStudentLinks
                .Where(psl => psl.SchoolId == schoolId && psl.ParentId == userId)
                .Join(_context.Students, psl => psl.StudentId, s => s.Id, (psl, s) => s.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(e => studentClassIds.Contains(e.ClassId) && e.IsSchedulePublished);
        }
        else if (role == "Teacher")
        {
            // Teachers see exams for any class they're assigned to (not just class
            // teacher) — drafts are visible only to the creator via ClassTeacher.
            // Matches the visibility rules of GetHomework.
            var assignedClassIds = await _context.TeacherClassAssignments
                .Where(tca => tca.SchoolId == schoolId && tca.TeacherId == userId)
                .Select(tca => tca.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var classTeacherClassIds = await _context.TeacherClassAssignments
                .Where(tca => tca.SchoolId == schoolId && tca.TeacherId == userId && tca.IsClassTeacher)
                .Select(tca => tca.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(e =>
                (assignedClassIds.Contains(e.ClassId) && e.IsSchedulePublished)
                || classTeacherClassIds.Contains(e.ClassId));
        }
        // Admin sees everything (query filters by school already).

        if (request.ClassId.HasValue)
        {
            query = query.Where(e => e.ClassId == request.ClassId.Value);
        }

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExamListItemDto(
                e.Id,
                e.ClassId,
                e.Class != null ? e.Class.Name : string.Empty,
                e.Class != null ? e.Class.Section : string.Empty,
                e.Name,
                e.AcademicYear,
                e.IsSchedulePublished,
                e.SchedulePublishedAt,
                e.IsResultsFinalized,
                e.ResultsFinalizedAt,
                e.Subjects.Count,
                e.Subjects.Any() ? (DateOnly?)e.Subjects.Min(s => s.ExamDate) : null,
                e.Subjects.Any() ? (DateOnly?)e.Subjects.Max(s => s.ExamDate) : null,
                e.CreatedAt))
            .ToListAsync(cancellationToken);

        return items;
    }
}
