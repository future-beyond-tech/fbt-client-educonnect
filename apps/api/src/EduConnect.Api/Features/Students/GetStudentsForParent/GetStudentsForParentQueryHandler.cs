using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Students.GetStudentsForParent;

public class GetStudentsForParentQueryHandler : IRequestHandler<GetStudentsForParentQuery, List<ParentChildDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetStudentsForParentQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ParentChildDto>> Handle(GetStudentsForParentQuery request, CancellationToken cancellationToken)
    {
        // parent_id is ALWAYS from JWT — never from request parameters (IDOR prevention)
        if (_currentUserService.Role != "Parent")
        {
            throw new ForbiddenException("This endpoint is for parents only.");
        }

        var children = await _context.ParentStudentLinks
            .Where(psl =>
                psl.SchoolId == _currentUserService.SchoolId &&
                psl.ParentId == _currentUserService.UserId)
            .Include(psl => psl.Student)
                .ThenInclude(s => s!.Class)
            .Where(psl => psl.Student != null && psl.Student.IsActive)
            .OrderBy(psl => psl.Student!.Name)
            .Select(psl => new ParentChildDto(
                psl.Student!.Id,
                psl.Student.Name,
                psl.Student.RollNumber,
                psl.Student.ClassId,
                psl.Student.Class != null ? psl.Student.Class.Name : string.Empty,
                psl.Student.Class != null ? psl.Student.Class.Section : string.Empty,
                psl.Relationship,
                psl.Student.IsActive))
            .ToListAsync(cancellationToken);

        return children;
    }
}
