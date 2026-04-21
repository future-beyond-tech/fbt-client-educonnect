using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Drains the in-memory scan queue, streams each attachment through the
/// configured <see cref="IAttachmentScanner"/> and updates its status.
///
/// Concurrency: single consumer. Each scan happens sequentially; if clamd
/// capacity becomes a bottleneck the queue can grow until the channel's
/// bounded limit applies backpressure to uploaders.
///
/// Ownership: scans bypass RLS (tenant unknown in a background context)
/// because the worker touches its own namespace (one row by ID, found in
/// any tenant). The handler intentionally opens its own DbContext scope.
/// </summary>
public sealed class AttachmentScanWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAttachmentScanQueue _queue;
    private readonly ILogger<AttachmentScanWorker> _logger;

    public AttachmentScanWorker(
        IServiceScopeFactory scopeFactory,
        IAttachmentScanQueue queue,
        ILogger<AttachmentScanWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AttachmentScanWorker started; listening for scan jobs.");

        try
        {
            await foreach (var attachmentId in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    await ScanOneAsync(attachmentId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Last-ditch: keep the worker alive on any unexpected
                    // failure so a poisonous job doesn't tank the queue.
                    _logger.LogError(ex,
                        "Attachment scan worker failed unexpectedly for {AttachmentId}",
                        attachmentId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    internal async Task ScanOneAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var scanner = scope.ServiceProvider.GetRequiredService<IAttachmentScanner>();

        // IgnoreQueryFilters: we don't have a tenant context in the worker
        // scope, so the EF global query filter would otherwise return
        // nothing. RLS is handled separately — the worker connection
        // doesn't set app.current_school_id so policies default-allow
        // (matching the anonymous-path contract from Phase 4).
        var attachment = await db.Attachments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

        if (attachment is null)
        {
            _logger.LogWarning("Scan job references missing attachment {AttachmentId}", attachmentId);
            return;
        }

        if (attachment.Status != AttachmentStatus.Pending)
        {
            _logger.LogInformation(
                "Skipping scan for attachment {AttachmentId}: status is already {Status}",
                attachmentId, attachment.Status);
            return;
        }

        // Buffer so the magic-byte pre-check can read a prefix without
        // depriving ClamAV of any bytes. 10 MB file cap (see
        // StorageOptions.MaxFileSizeBytes) keeps the memory budget bounded.
        await using var buffered = new MemoryStream();
        try
        {
            await using var source = await storage.OpenObjectReadStreamAsync(attachment.StorageKey, ct);
            await source.CopyToAsync(buffered, ct);
            buffered.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not stream attachment {AttachmentId} from storage for scanning",
                attachmentId);
            attachment.Status = AttachmentStatus.ScanFailed;
            attachment.ScannedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var prefixLength = (int)Math.Min(buffered.Length, 64);
        var prefix = new byte[prefixLength];
        _ = await buffered.ReadAsync(prefix.AsMemory(0, prefixLength), ct);
        buffered.Position = 0;

        if (!MimeSignatureValidator.IsConsistent(prefix, attachment.ContentType))
        {
            _logger.LogWarning(
                "Attachment {AttachmentId} declared {ContentType} but magic bytes don't match; marking Infected (MIME_MISMATCH).",
                attachmentId, attachment.ContentType);

            attachment.Status = AttachmentStatus.Infected;
            attachment.ThreatName = "MIME_MISMATCH";
            attachment.ScannedAt = DateTimeOffset.UtcNow;

            try
            {
                await storage.DeleteObjectAsync(attachment.StorageKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete MIME-mismatch object {Key}; DB row will stay Infected for operator cleanup",
                    attachment.StorageKey);
            }

            await db.SaveChangesAsync(ct);
            return;
        }

        ScanResult result;
        try
        {
            result = await scanner.ScanAsync(buffered, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Scanner threw for attachment {AttachmentId}",
                attachmentId);
            attachment.Status = AttachmentStatus.ScanFailed;
            attachment.ScannedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        attachment.ScannedAt = DateTimeOffset.UtcNow;

        if (result.IsClean)
        {
            attachment.Status = AttachmentStatus.Available;
            _logger.LogInformation(
                "Attachment {AttachmentId} cleared by {Engine}",
                attachmentId, result.Engine);
        }
        else if (result.IsInfected)
        {
            attachment.Status = AttachmentStatus.Infected;
            attachment.ThreatName = result.ThreatName;

            _logger.LogWarning(
                "Attachment {AttachmentId} flagged as infected by {Engine}: {Threat}. Deleting from storage.",
                attachmentId, result.Engine, result.ThreatName);

            try
            {
                await storage.DeleteObjectAsync(attachment.StorageKey, ct);
            }
            catch (Exception ex)
            {
                // Keep the DB flag set; operator can clean up storage.
                _logger.LogError(ex,
                    "Failed to remove infected object {Key} from storage; status remains Infected",
                    attachment.StorageKey);
            }
        }
        else
        {
            attachment.Status = AttachmentStatus.ScanFailed;
            attachment.ThreatName = result.ThreatName;
            _logger.LogError(
                "Attachment {AttachmentId} scan failed via {Engine}: {Detail}",
                attachmentId, result.Engine, result.ThreatName);
        }

        await db.SaveChangesAsync(ct);
    }
}
