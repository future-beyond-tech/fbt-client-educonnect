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

        if ((request.TargetAudience == "Class" || request.TargetAudience == "Section") && !request.TargetClassId.HasValue)
        {
            throw new InvalidOperationException("Target class ID is required for class/section-specific notices.");
        }

        if (request.TargetClassId.HasValue)
        {
            var classExists = await _context.Classes
                .AnyAsync(c =>
                    c.Id == request.TargetClassId.Value &&
                    c.SchoolId == _currentUserService.SchoolId,
                    cancellationToken);

            if (!classExists)
            {
                _logger.LogWarning("Class {ClassId} not found in school {SchoolId}", request.TargetClassId, _currentUserService.SchoolId);
                throw new NotFoundException("Class", request.TargetClassId.Value.ToString());
            }
        }

        var notice = new NoticeEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Title = request.Title,
            Body = request.Body,
            TargetAudience = request.TargetAudience,
            TargetClassId = request.TargetClassId,
            PublishedById = _currentUserService.UserId,
            IsPublished = false,
            ExpiresAt = request.ExpiresAt,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Notices.Add(notice);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notice created (draft): {NoticeId} by admin {AdminId}",
            notice.Id, _currentUserService.UserId);

        return new CreateNoticeResponse(notice.Id, "Notice created as draft successfully.");
    }
}
