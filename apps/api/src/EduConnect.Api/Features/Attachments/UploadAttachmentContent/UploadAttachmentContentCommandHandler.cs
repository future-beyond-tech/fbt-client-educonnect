using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Features.Attachments.UploadAttachmentContent;

public class UploadAttachmentContentCommandHandler
    : IRequestHandler<UploadAttachmentContentCommand, UploadAttachmentContentResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly ILogger<UploadAttachmentContentCommandHandler> _logger;

    public UploadAttachmentContentCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService,
        ILogger<UploadAttachmentContentCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<UploadAttachmentContentResponse> Handle(
        UploadAttachmentContentCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and teachers can upload attachments.");
        }

        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(
                a => a.Id == request.AttachmentId &&
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

        if (request.SizeBytes != attachment.SizeBytes)
        {
            throw new InvalidOperationException("Uploaded file size does not match the prepared attachment.");
        }

        await _storageService.EnsureUploadTargetAvailableAsync(cancellationToken);
        await _storageService.UploadObjectAsync(
            attachment.StorageKey,
            request.Content,
            attachment.ContentType,
            request.SizeBytes,
            cancellationToken);

        _logger.LogInformation(
            "Attachment content uploaded via API fallback for attachment {AttachmentId} by user {UserId}",
            attachment.Id,
            _currentUserService.UserId);

        return new UploadAttachmentContentResponse("File uploaded successfully.");
    }
}
