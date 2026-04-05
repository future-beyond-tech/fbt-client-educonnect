using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Attendance.GetAttendance;

public class GetAttendanceQueryHandler : IRequestHandler<GetAttendanceQuery, List<AttendanceDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetAttendanceQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<AttendanceDto>> Handle(GetAttendanceQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AttendanceRecords
            .Where(ar => ar.SchoolId == _currentUserService.SchoolId && !ar.IsDeleted)
            .AsQueryable();

        if (_currentUserService.Role == "Parent")
        {
            if (request.StudentId.HasValue)
            {
                var parentStudentLink = await _context.ParentStudentLinks
                    .FirstOrDefaultAsync(psl =>
                        psl.SchoolId == _currentUserService.SchoolId &&
                        psl.ParentId == _currentUserService.UserId &&
                        psl.StudentId == request.StudentId.Value,
                        cancellationToken);

                if (parentStudentLink != null)
                {
                    query = query.Where(ar => ar.StudentId == request.StudentId.Value);
                }
                else
                {
                    return [];
                }
            }
            else
            {
                var linkedStudentIds = await _context.ParentStudentLinks
                    .Where(psl =>
                        psl.SchoolId == _currentUserService.SchoolId &&
                        psl.ParentId == _currentUserService.UserId)
                    .Select(psl => psl.StudentId)
                    .ToListAsync(cancellationToken);

                query = query.Where(ar => linkedStudentIds.Contains(ar.StudentId));
            }
        }
        else if (_currentUserService.Role == "Teacher")
        {
            var assignedClassIds = await _context.TeacherClassAssignments
                .Where(tca =>
                    tca.SchoolId == _currentUserService.SchoolId &&
                    tca.TeacherId == _currentUserService.UserId)
                .Select(tca => tca.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (assignedClassIds.Count == 0)
            {
                return [];
            }

            if (request.StudentId.HasValue)
            {
                var hasAccessToStudent = await _context.Students
                    .AnyAsync(student =>
                        student.Id == request.StudentId.Value &&
                        assignedClassIds.Contains(student.ClassId),
                        cancellationToken);

                if (!hasAccessToStudent)
                {
                    throw new ForbiddenException("You can only view attendance for students in your assigned classes.");
                }

                query = query.Where(ar => ar.StudentId == request.StudentId.Value);
            }
            else
            {
                var assignedStudentIds = await _context.Students
                    .Where(student => assignedClassIds.Contains(student.ClassId))
                    .Select(student => student.Id)
                    .ToListAsync(cancellationToken);

                query = query.Where(ar => assignedStudentIds.Contains(ar.StudentId));
            }
        }
        else if (_currentUserService.Role == "Admin" && request.StudentId.HasValue)
        {
            var studentExists = await _context.Students
                .AnyAsync(student => student.Id == request.StudentId.Value, cancellationToken);

            if (!studentExists)
            {
                return [];
            }

            query = query.Where(ar => ar.StudentId == request.StudentId.Value);
        }

        if (request.Month.HasValue && request.Year.HasValue)
        {
            query = query.Where(ar =>
                ar.Date.Month == request.Month.Value &&
                ar.Date.Year == request.Year.Value);
        }

        var records = await query
            .OrderByDescending(ar => ar.Date)
            .Select(ar => new AttendanceDto(
                ar.Id,
                ar.StudentId,
                ar.Date,
                ar.Status,
                ar.Reason,
                ar.EnteredByRole,
                ar.CreatedAt))
            .ToListAsync(cancellationToken);

        return records;
    }
}
