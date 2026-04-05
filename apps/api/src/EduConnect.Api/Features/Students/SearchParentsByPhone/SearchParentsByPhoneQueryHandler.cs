using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
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
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can search for parents.");
        }

        if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Trim().Length < 3)
        {
            return [];
        }

        var phoneTrimmed = request.Phone.Trim();

        var parents = await _context.Users
            .Where(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Role == "Parent" &&
                u.IsActive &&
                u.Phone.Contains(phoneTrimmed))
            .OrderBy(u => u.Name)
            .Take(10)
            .Select(u => new ParentSearchResultDto(u.Id, u.Name, u.Phone))
            .ToListAsync(cancellationToken);

        return parents;
    }
}
