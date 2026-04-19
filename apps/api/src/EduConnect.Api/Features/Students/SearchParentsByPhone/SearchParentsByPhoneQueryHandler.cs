using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Students.SearchParentsByPhone;

public class SearchParentsByPhoneQueryHandler : IRequestHandler<SearchParentsByPhoneQuery, List<ParentSearchResultDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;

    public SearchParentsByPhoneQueryHandler(AppDbContext context, CurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ParentSearchResultDto>> Handle(SearchParentsByPhoneQuery request, CancellationToken cancellationToken)
    {
        // Admins and class teachers both need parent lookup so they can link
        // an existing parent (e.g. a sibling already enrolled) during student
        // enrollment. Teachers who aren't class teachers are still blocked by
        // EnrollStudentCommandHandler, so read access here is safe.
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and class teachers can search for parents.");
        }

        var phoneSearch = JapanPhoneNumber.NormalizeSearchTerm(request.Phone);

        if (phoneSearch.Length < 3)
        {
            return [];
        }

        var parents = await _context.Users
            .Where(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Role == "Parent" &&
                u.IsActive &&
                u.Phone.Contains(phoneSearch))
            .OrderBy(u => u.Name)
            .Take(10)
            .Select(u => new ParentSearchResultDto(
                u.Id,
                u.Name,
                u.Phone,
                u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);

        return parents;
    }
}
