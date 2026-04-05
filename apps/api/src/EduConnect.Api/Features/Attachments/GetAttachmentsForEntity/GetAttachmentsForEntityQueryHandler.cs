using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Services;
using MediatR;

namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public class GetAttachmentsForEntityQueryHandler : IRequestHandler<GetAttachmentsForEntityQuery, List<AttachmentDto>>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly IStorageService _storageService;

    public GetAttachmentsForEntityQueryHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        IStorageService storageService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _storageService = storageService;
    }

    public async Task<List<AttachmentDto>> Handle(GetAttachmentsForEntityQuery request, CancellationToken cancellationToken)
    {
        var attachments = await _context.Attachments
            .Where(a =>
                a.EntityId == request.EntityId &&
                a.EntityType == request.EntityType &&
                a.SchoolId == _currentUserService.SchoolId)
            .OrderBy(a => a.UploadedAt)
            .ToListAsync(cancellationToken);

        var result = new List<AttachmentDto>(attachments.Count);

        foreach (var attachment in attachments)
        {
            // Generate 1-hour presigned download URL
            var downloadUrl = await _storageService.GeneratePresignedDownloadUrlAsync(
                attachment.StorageKey,
                TimeSpan.FromHours(1),
                cancellationToken);

            result.Add(new AttachmentDto(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                downloadUrl,
                attachment.UploadedAt));
        }

        return result;
    }
}
