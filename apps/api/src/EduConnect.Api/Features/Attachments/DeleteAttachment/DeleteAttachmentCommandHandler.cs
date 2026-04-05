using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attachments.DeleteAttachment;

public class DeleteAttachmentCommandHandler : IRequestHandler<DeleteAttachmentCommand, DeleteAttachmentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly ILogger<DeleteAttachmentCommandHandler> _logger;

    public DeleteAttachmentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService,
        ILogger<DeleteAttachmentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<DeleteAttachmentResponse> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and teachers can delete attachments.");
        }

        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a =>
                a.Id == request.AttachmentId &&
                a.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (attachment == null)
        {
            throw new NotFoundException("Attachment", request.AttachmentId.ToString());
        }

        // Teachers can only delete their own attachments; admins can delete any
        if (_currentUserService.Role == "Teacher" && attachment.UploadedById != _currentUserService.UserId)
        {
            throw new ForbiddenException("You can only delete your own attachments.");
        }

        // If attached to a published notice, block deletion
        if (attachment.EntityType == "notice" && attachment.EntityId.HasValue)
        {
            var notice = await _context.Notices
                .FirstOrDefaultAsync(n => n.Id == attachment.EntityId && n.IsPublished, cancellationToken);

            if (notice != null)
            {
                throw new InvalidOperationException("Cannot delete attachments from a published notice.");
            }
        }

        // Delete from storage
        await _storageService.DeleteObjectAsync(attachment.StorageKey, cancellationToken);

        // Remove DB record
        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Attachment {AttachmentId} deleted by user {UserId}",
            request.AttachmentId, _currentUserService.UserId);

        return new DeleteAttachmentResponse("Attachment deleted successfully.");
    }
}
