using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrl;

public class RequestUploadUrlCommandHandler : IRequestHandler<RequestUploadUrlCommand, RequestUploadUrlResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly ILogger<RequestUploadUrlCommandHandler> _logger;

    public RequestUploadUrlCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService,
        ILogger<RequestUploadUrlCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<RequestUploadUrlResponse> Handle(RequestUploadUrlCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and teachers can upload attachments.");
        }

        var attachmentId = Guid.NewGuid();
        var extension = Path.GetExtension(request.FileName);
        var storageKey = $"{_currentUserService.SchoolId}/{attachmentId}{extension}";

        // Create attachment record (entity_id/entity_type null until attached)
        var attachment = new AttachmentEntity
        {
            Id = attachmentId,
            SchoolId = _currentUserService.SchoolId,
            EntityId = null,
            EntityType = null,
            StorageKey = storageKey,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            UploadedById = _currentUserService.UserId,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);

        // Generate presigned upload URL (15 min expiry)
        var uploadUrl = await _storageService.GeneratePresignedUploadUrlAsync(
            storageKey,
            request.ContentType,
            request.SizeBytes,
            TimeSpan.FromMinutes(15),
            cancellationToken);

        _logger.LogInformation(
            "Upload URL generated: attachment {AttachmentId} by user {UserId}",
            attachmentId, _currentUserService.UserId);

        return new RequestUploadUrlResponse(uploadUrl, storageKey, attachmentId);
    }
}
