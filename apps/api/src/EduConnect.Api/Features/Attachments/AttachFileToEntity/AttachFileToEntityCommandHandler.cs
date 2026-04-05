using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Attachments.AttachFileToEntity;

public class AttachFileToEntityCommandHandler : IRequestHandler<AttachFileToEntityCommand, AttachFileToEntityResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<AttachFileToEntityCommandHandler> _logger;

    private const int MaxAttachmentsPerEntity = 5;

    public AttachFileToEntityCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<AttachFileToEntityCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<AttachFileToEntityResponse> Handle(AttachFileToEntityCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and teachers can attach files.");
        }

        // Verify attachment exists, belongs to current user, and is not yet attached
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a =>
                a.Id == request.AttachmentId &&
                a.SchoolId == _currentUserService.SchoolId &&
                a.UploadedById == _currentUserService.UserId,
                cancellationToken);

        if (attachment == null)
        {
            throw new NotFoundException("Attachment", request.AttachmentId.ToString());
        }

        if (attachment.EntityId.HasValue)
        {
            throw new InvalidOperationException("This attachment is already associated with an entity.");
        }

        // Verify the target entity exists in the correct table
        if (request.EntityType == "homework")
        {
            var homework = await _context.Homeworks
                .FirstOrDefaultAsync(h => h.Id == request.EntityId && h.SchoolId == _currentUserService.SchoolId && !h.IsDeleted, cancellationToken);

            if (homework == null)
            {
                throw new NotFoundException("Homework", request.EntityId.ToString());
            }
        }
        else if (request.EntityType == "notice")
        {
            var notice = await _context.Notices
                .FirstOrDefaultAsync(n => n.Id == request.EntityId && n.SchoolId == _currentUserService.SchoolId && !n.IsDeleted, cancellationToken);

            if (notice == null)
            {
                throw new NotFoundException("Notice", request.EntityId.ToString());
            }
        }

        // Check max 5 attachments per entity
        var existingCount = await _context.Attachments
            .CountAsync(a =>
                a.EntityId == request.EntityId &&
                a.EntityType == request.EntityType &&
                a.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (existingCount >= MaxAttachmentsPerEntity)
        {
            throw new InvalidOperationException($"Maximum of {MaxAttachmentsPerEntity} attachments per entity reached.");
        }

        // Associate the attachment
        attachment.EntityId = request.EntityId;
        attachment.EntityType = request.EntityType;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Attachment {AttachmentId} linked to {EntityType} {EntityId}",
            request.AttachmentId, request.EntityType, request.EntityId);

        return new AttachFileToEntityResponse("File attached successfully.");
    }
}
