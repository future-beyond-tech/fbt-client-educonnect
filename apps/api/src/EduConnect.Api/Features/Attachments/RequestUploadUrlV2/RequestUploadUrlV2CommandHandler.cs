using System.Text;
using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Features.Attachments.RequestUploadUrlV2;

public class RequestUploadUrlV2CommandHandler : IRequestHandler<RequestUploadUrlV2Command, RequestUploadUrlV2Response>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<RequestUploadUrlV2CommandHandler> _logger;

    public RequestUploadUrlV2CommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService,
        IOptions<StorageOptions> storageOptions,
        ILogger<RequestUploadUrlV2CommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<RequestUploadUrlV2Response> Handle(RequestUploadUrlV2Command request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "Teacher")
        {
            throw new ForbiddenException("Only admins and teachers can upload attachments.");
        }

        var sanitizedFileName = SanitizeFileName(request.FileName);
        var extension = Path.GetExtension(sanitizedFileName);
        var attachmentId = Guid.NewGuid();
        var storageKey = $"{_currentUserService.SchoolId}/{request.EntityType}/{attachmentId}-{sanitizedFileName}";

        var attachment = new AttachmentEntity
        {
            Id = attachmentId,
            SchoolId = _currentUserService.SchoolId,
            EntityId = null,
            EntityType = request.EntityType,
            StorageKey = storageKey,
            FileName = sanitizedFileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            UploadedById = _currentUserService.UserId,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);

        var uploadUrl = await _storageService.GeneratePresignedUploadUrlAsync(
            storageKey,
            request.ContentType,
            request.SizeBytes,
            TimeSpan.FromMinutes(_storageOptions.PresignedUploadExpiryMinutes),
            cancellationToken);

        _logger.LogInformation(
            "Upload URL v2 generated: attachment {AttachmentId} for {EntityType} by user {UserId} with extension {Extension}",
            attachmentId,
            request.EntityType,
            _currentUserService.UserId,
            extension);

        return new RequestUploadUrlV2Response(uploadUrl, attachmentId);
    }

    private static string SanitizeFileName(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return "file";
        }

        var builder = new StringBuilder(safeFileName.Length);
        foreach (var character in safeFileName)
        {
            builder.Append(Path.GetInvalidFileNameChars().Contains(character)
                ? '-'
                : character);
        }

        return builder.ToString();
    }
}
