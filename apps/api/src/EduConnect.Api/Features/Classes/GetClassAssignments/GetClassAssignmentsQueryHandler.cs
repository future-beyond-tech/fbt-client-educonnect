using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Classes.GetClassAssignments;

public class GetClassAssignmentsQueryHandler : IRequestHandler<GetClassAssignmentsQuery, List<ClassAssignmentDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetClassAssignmentsQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ClassAssignmentDto>> Handle(GetClassAssignmentsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can view class assignments.");
        }

        var classExists = await _context.Classes
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == request.ClassId &&
                c.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (!classExists)
        {
            throw new NotFoundException($"Class with ID {request.ClassId} not found.");
        }

        var items = await _context.TeacherClassAssignments
            .AsNoTracking()
            .Where(tca =>
                tca.SchoolId == _currentUserService.SchoolId &&
                tca.ClassId == request.ClassId)
            .Join(
                _context.Users.AsNoTracking(),
                tca => tca.TeacherId,
                u => u.Id,
                (tca, u) => new { tca, u })
            .OrderByDescending(x => x.tca.IsClassTeacher)
            .ThenBy(x => x.tca.Subject)
            .ThenBy(x => x.u.Name)
            .Select(x => new ClassAssignmentDto(
                x.tca.Id,
                x.tca.TeacherId,
                x.u.Name,
                x.u.Phone,
                x.tca.Subject,
                x.tca.IsClassTeacher,
                x.tca.CreatedAt))
            .ToListAsync(cancellationToken);

        return items;
    }
}

