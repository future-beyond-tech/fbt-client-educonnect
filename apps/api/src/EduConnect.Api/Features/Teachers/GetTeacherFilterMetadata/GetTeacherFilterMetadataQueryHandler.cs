using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Teachers.GetTeacherFilterMetadata;

public class GetTeacherFilterMetadataQueryHandler
    : IRequestHandler<GetTeacherFilterMetadataQuery, TeacherFilterMetadataDto>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public GetTeacherFilterMetadataQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<TeacherFilterMetadataDto> Handle(
        GetTeacherFilterMetadataQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can read staff filter metadata.");
        }

        var schoolId = _currentUserService.SchoolId;

        var subjects = await _context.TeacherClassAssignments
            .Where(tca => tca.SchoolId == schoolId && tca.Subject != string.Empty)
            .Select(tca => tca.Subject)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

        return new TeacherFilterMetadataDto(subjects);
    }
}
