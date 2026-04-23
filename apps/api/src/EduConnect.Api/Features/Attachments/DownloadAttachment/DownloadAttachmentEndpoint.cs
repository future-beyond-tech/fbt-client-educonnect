using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Features.Attachments.DownloadAttachment;

/// <summary>
/// Audit-logged download redirect. Loads the attachment, re-runs the
/// same access check that <see cref="GetAttachmentsForEntityQueryHandler"/>
/// applies, logs the download attempt at Information with the requesting
/// user, and 302s to a freshly-minted presigned URL. Used for the
/// entity types where compliance demands a per-download server-side
/// audit trail (homework_submission and notice); homework continues to
/// hand out direct presigned URLs since the surface is lower-stakes.
/// </summary>
public static class DownloadAttachmentEndpoint
{
    public static async Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromQuery] string? download,
        IMediator mediator,
        AppDbContext context,
        CurrentUserService currentUser,
        IStorageService storage,
        IOptions<StorageOptions> storageOptions,
        ILogger<DownloadAttachmentLog> logger,
        CancellationToken cancellationToken)
    {
        var attachment = await context.Attachments
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                a.SchoolId == currentUser.SchoolId,
                cancellationToken);

        if (attachment is null)
        {
            // Same shape as a not-found from the GET handler — never
            // confirm existence to a caller that lacks access.
            return Results.NotFound();
        }

        if (attachment.EntityId is null || attachment.EntityType is null)
        {
            // Unattached row: caller is the uploader by definition (the
            // GET handler doesn't expose unattached rows). Skip the
            // entity-scoped access check and rely on tenant scoping.
            if (attachment.UploadedById != currentUser.UserId &&
                currentUser.Role != "Admin")
            {
                return Results.NotFound();
            }
        }
        else
        {
            // Re-run the entity-level access check by dispatching the
            // existing query. Anything not visible to this caller via
            // GetAttachmentsForEntity is also off-limits here.
            List<AttachmentDto> visible;
            try
            {
                visible = await mediator.Send(
                    new GetAttachmentsForEntityQuery(attachment.EntityId.Value, attachment.EntityType),
                    cancellationToken);
            }
            catch (ForbiddenException)
            {
                logger.LogInformation(
                    "Attachment download denied (forbidden): {AttachmentId} by user {UserId} in school {SchoolId}",
                    attachment.Id, currentUser.UserId, currentUser.SchoolId);
                return Results.NotFound();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }

            if (!visible.Any(v => v.Id == attachment.Id))
            {
                logger.LogInformation(
                    "Attachment download denied (not visible): {AttachmentId} by user {UserId} in school {SchoolId}",
                    attachment.Id, currentUser.UserId, currentUser.SchoolId);
                return Results.NotFound();
            }
        }

        if (attachment.Status != AttachmentStatus.Available)
        {
            // Pending / Infected / ScanFailed have no usable presigned
            // URL. The GET handler already withholds the URL field for
            // these statuses; mirror that here.
            return Results.NotFound();
        }

        var presignedUrl = await storage.GeneratePresignedDownloadUrlAsync(
            attachment.StorageKey,
            TimeSpan.FromMinutes(storageOptions.Value.PresignedDownloadExpiryMinutes),
            attachment.FileName,
            attachment.ContentType,
            ShouldForceDownload(download) || AttachmentFeatureRules.RequiresForcedDownload(attachment.ContentType),
            cancellationToken);

        // The audit signal: one log line per download with user + tenant +
        // attachment + entity context. Picked up by the existing
        // structured-logging pipeline (Serilog → file/Sentry).
        logger.LogInformation(
            "Attachment download: {AttachmentId} ({EntityType} {EntityId}) by user {UserId} in school {SchoolId}",
            attachment.Id,
            attachment.EntityType,
            attachment.EntityId,
            currentUser.UserId,
            currentUser.SchoolId);

        return Results.Redirect(presignedUrl, permanent: false);
    }

    private static bool ShouldForceDownload(string? download) =>
        download is not null &&
        (download.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         download.Equals("1", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Empty marker class so we get a discriminating
/// <see cref="ILogger{TCategoryName}"/> category in Serilog output.
/// </summary>
public sealed class DownloadAttachmentLog;
