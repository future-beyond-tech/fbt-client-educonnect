using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Notices.CreateNotice;

public class CreateNoticeCommandHandler : IRequestHandler<CreateNoticeCommand, CreateNoticeResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<CreateNoticeCommandHandler> _logger;

    public CreateNoticeCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<CreateNoticeCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CreateNoticeResponse> Handle(CreateNoticeCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted to create notice",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only administrators can create notices.");
        }

        var targetClasses = await ResolveTargetClassesAsync(request, cancellationToken);

        var notice = new NoticeEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            TargetAudience = request.TargetAudience,
            PublishedById = _currentUserService.UserId,
            IsPublished = false,
            ExpiresAt = request.ExpiresAt,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Notices.Add(notice);

        if (targetClasses.Count > 0)
        {
            _context.NoticeTargetClasses.AddRange(targetClasses.Select(targetClass => new NoticeTargetClassEntity
            {
                NoticeId = notice.Id,
                ClassId = targetClass.Id,
                SchoolId = _currentUserService.SchoolId,
                CreatedAt = DateTimeOffset.UtcNow
            }));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notice created (draft): {NoticeId} by admin {AdminId}",
            notice.Id, _currentUserService.UserId);

        return new CreateNoticeResponse(notice.Id, "Notice created as draft successfully.");
    }

    private async Task<List<ClassEntity>> ResolveTargetClassesAsync(
        CreateNoticeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TargetAudience == "All")
        {
            return [];
        }

        var targetClassIds = request.TargetClassIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];

        if (targetClassIds.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "TargetClassIds", ["Select at least one class section for targeted notices."] }
            });
        }

        var targetClasses = await _context.Classes
            .Where(c =>
                c.SchoolId == _currentUserService.SchoolId &&
                targetClassIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (targetClasses.Count != targetClassIds.Count)
        {
            _logger.LogWarning(
                "One or more selected notice target classes were not found in school {SchoolId}",
                _currentUserService.SchoolId);
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "TargetClassIds", ["One or more selected class sections were not found."] }
            });
        }

        var classGroups = targetClasses
            .Select(c => (c.Name, c.AcademicYear))
            .Distinct()
            .ToList();

        if (classGroups.Count != 1)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "TargetClassIds", ["Select sections from a single class and academic year."] }
            });
        }

        if (request.TargetAudience == "Class")
        {
            var (className, academicYear) = classGroups[0];
            var expectedClassIds = await _context.Classes
                .Where(c =>
                    c.SchoolId == _currentUserService.SchoolId &&
                    c.Name == className &&
                    c.AcademicYear == academicYear)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var missingSections = expectedClassIds.Except(targetClassIds).ToList();
            if (missingSections.Count > 0)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "TargetClassIds", ["Class-wide notices must include all sections for the selected class."] }
                });
            }
        }

        return targetClasses;
    }
}
