using EduConnect.Api.Common.Auth;
using EduConnect.Api.Features.Attachments;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services.Scanning;
using MediatR;

namespace EduConnect.Api.Features.Attachments.AttachFileToEntity;

public class AttachFileToEntityCommandHandler : IRequestHandler<AttachFileToEntityCommand, AttachFileToEntityResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IAttachmentScanQueue _scanQueue;
    private readonly ILogger<AttachFileToEntityCommandHandler> _logger;

    public AttachFileToEntityCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IAttachmentScanQueue scanQueue,
        ILogger<AttachFileToEntityCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _scanQueue = scanQueue;
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

        if (!string.IsNullOrWhiteSpace(attachment.EntityType) &&
            !string.Equals(attachment.EntityType, request.EntityType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This attachment was prepared for a different entity type.");
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

            if (_currentUserService.Role == "Teacher")
            {
                if (homework.AssignedById != _currentUserService.UserId)
                {
                    throw new ForbiddenException("You can only attach files to homework you created.");
                }

                if (homework.Status != "Draft" && homework.Status != "Rejected")
                {
                    throw new ForbiddenException("Attachments can only be changed while homework is editable.");
                }
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

            if (notice.IsPublished)
            {
                throw new ForbiddenException("Attachments can only be changed before the notice is published.");
            }
        }

        // Check max 5 attachments per entity
        var existingCount = await _context.Attachments
            .CountAsync(a =>
                a.EntityId == request.EntityId &&
                a.EntityType == request.EntityType &&
                a.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (existingCount >= AttachmentFeatureRules.MaxAttachmentsPerEntity)
        {
            throw new InvalidOperationException($"Maximum of {AttachmentFeatureRules.MaxAttachmentsPerEntity} attachments per entity reached.");
        }

        // Associate the attachment
        attachment.EntityId = request.EntityId;
        attachment.EntityType = request.EntityType;

        await _context.SaveChangesAsync(cancellationToken);

        // Phase 5 — only enqueue a scan for attachments that haven't been
        // processed yet. Re-attaching a previously-Available file (e.g. the
        // client retries) must not reset state back to Pending.
        if (attachment.Status == AttachmentStatus.Pending)
        {
            await _scanQueue.EnqueueAsync(attachment.Id, cancellationToken);
            _logger.LogInformation(
                "Attachment {AttachmentId} enqueued for virus scan",
                attachment.Id);
        }

        _logger.LogInformation(
            "Attachment {AttachmentId} linked to {EntityType} {EntityId}",
            request.AttachmentId, request.EntityType, request.EntityId);

        return new AttachFileToEntityResponse("File attached successfully.");
    }
}
