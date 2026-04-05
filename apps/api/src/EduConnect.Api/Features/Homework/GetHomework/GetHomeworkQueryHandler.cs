using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Homework.GetHomework;

public class GetHomeworkQueryHandler : IRequestHandler<GetHomeworkQuery, List<HomeworkDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetHomeworkQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<HomeworkDto>> Handle(GetHomeworkQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Homeworks
            .Where(h => h.SchoolId == _currentUserService.SchoolId && !h.IsDeleted)
            .AsQueryable();

        if (_currentUserService.Role == "Parent")
        {
            var studentClassIds = await _context.ParentStudentLinks
                .Where(psl =>
                    psl.SchoolId == _currentUserService.SchoolId &&
                    psl.ParentId == _currentUserService.UserId)
                .Join(_context.Students, psl => psl.StudentId, s => s.Id, (psl, s) => s.ClassId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(h => studentClassIds.Contains(h.ClassId));
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

            query = query.Where(h => assignedClassIds.Contains(h.ClassId));
        }

        if (request.ClassId.HasValue)
        {
            query = query.Where(h => h.ClassId == request.ClassId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Subject))
        {
            query = query.Where(h => h.Subject == request.Subject);
        }

        var homeworks = await query
            .OrderByDescending(h => h.DueDate)
            .Select(h => new HomeworkDto(
                h.Id,
                h.ClassId,
                h.Subject,
                h.Title,
                h.Description,
                h.DueDate,
                h.IsEditable,
                h.PublishedAt ?? DateTimeOffset.UtcNow))
            .ToListAsync(cancellationToken);

        return homeworks;
    }
}
