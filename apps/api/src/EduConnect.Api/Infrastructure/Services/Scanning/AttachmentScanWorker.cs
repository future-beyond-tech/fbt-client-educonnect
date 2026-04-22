using System.Net.Sockets;
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
    // Total scan attempts on transient I/O failures (network blip, daemon
    // restart, request timeout). 3 attempts with 2/4s back-offs covers the
    // typical clamd restart window without unduly stalling other jobs.
    internal const int MaxScanAttempts = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAttachmentScanQueue _queue;
    private readonly ILogger<AttachmentScanWorker> _logger;
    private readonly Func<int, TimeSpan> _retryBackoff;

    public AttachmentScanWorker(
        IServiceScopeFactory scopeFactory,
        IAttachmentScanQueue queue,
        ILogger<AttachmentScanWorker> logger)
        : this(scopeFactory, queue, logger,
            // Exponential back-off: 2s after the 1st failure, 4s after the
            // 2nd, never reached after the 3rd (loop exits).
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))
    {
    }

    // Internal ctor lets tests inject a zero back-off so they don't sit
    // through 6+ seconds of Task.Delay per retry exhaustion case.
    internal AttachmentScanWorker(
        IServiceScopeFactory scopeFactory,
        IAttachmentScanQueue queue,
        ILogger<AttachmentScanWorker> logger,
        Func<int, TimeSpan> retryBackoff)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
        _retryBackoff = retryBackoff;
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
        var blockedNotifier = scope.ServiceProvider.GetRequiredService<IAttachmentBlockedNotifier>();

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
            attachment.ThreatName = "storage_open_failed";
            attachment.ScannedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await blockedNotifier.NotifyAsync(AttachmentBlockedKind.ScanFailed, attachment, ct);
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
            await blockedNotifier.NotifyAsync(AttachmentBlockedKind.Infected, attachment, ct);
            return;
        }

        ScanResult? result = null;
        Exception? lastTransientFailure = null;
        for (var attempt = 1; attempt <= MaxScanAttempts; attempt++)
        {
            try
            {
                buffered.Position = 0;
                result = await scanner.ScanAsync(buffered, ct);
                lastTransientFailure = null;
                break;
            }
            catch (Exception ex) when (IsTransientScannerFailure(ex))
            {
                lastTransientFailure = ex;
                if (attempt == MaxScanAttempts) break;

                var backoff = _retryBackoff(attempt);
                _logger.LogWarning(ex,
                    "Scanner transient failure (attempt {Attempt}/{Max}) for {AttachmentId}; retrying in {DelaySeconds}s",
                    attempt, MaxScanAttempts, attachmentId, backoff.TotalSeconds);
                await Task.Delay(backoff, ct);
            }
            catch (Exception ex)
            {
                // Non-transient (e.g. parse / protocol bug) — no point retrying.
                _logger.LogError(ex,
                    "Scanner failed (non-transient) for attachment {AttachmentId}",
                    attachmentId);
                attachment.Status = AttachmentStatus.ScanFailed;
                attachment.ThreatName = ex.GetType().Name;
                attachment.ScannedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await blockedNotifier.NotifyAsync(AttachmentBlockedKind.ScanFailed, attachment, ct);
                return;
            }
        }

        if (result is null)
        {
            _logger.LogError(lastTransientFailure,
                "Scanner failed after {Max} transient retries for attachment {AttachmentId}",
                MaxScanAttempts, attachmentId);
            attachment.Status = AttachmentStatus.ScanFailed;
            attachment.ThreatName = lastTransientFailure switch
            {
                TimeoutException   => "scan_timeout",
                SocketException    => "scanner_unreachable",
                IOException        => "scanner_io_error",
                _                  => lastTransientFailure?.GetType().Name ?? "unknown",
            };
            attachment.ScannedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await blockedNotifier.NotifyAsync(AttachmentBlockedKind.ScanFailed, attachment, ct);
            return;
        }

        attachment.ScannedAt = DateTimeOffset.UtcNow;

        var verdict = result;
        if (verdict.IsClean)
        {
            attachment.Status = AttachmentStatus.Available;
            _logger.LogInformation(
                "Attachment {AttachmentId} cleared by {Engine}",
                attachmentId, verdict.Engine);
        }
        else if (verdict.IsInfected)
        {
            attachment.Status = AttachmentStatus.Infected;
            attachment.ThreatName = verdict.ThreatName;

            _logger.LogWarning(
                "Attachment {AttachmentId} flagged as infected by {Engine}: {Threat}. Deleting from storage.",
                attachmentId, verdict.Engine, verdict.ThreatName);

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
            attachment.ThreatName = verdict.ThreatName;
            _logger.LogError(
                "Attachment {AttachmentId} scan failed via {Engine}: {Detail}",
                attachmentId, verdict.Engine, verdict.ThreatName);
        }

        await db.SaveChangesAsync(ct);

        if (attachment.Status == AttachmentStatus.Infected)
        {
            await blockedNotifier.NotifyAsync(AttachmentBlockedKind.Infected, attachment, ct);
        }
        else if (attachment.Status == AttachmentStatus.ScanFailed)
        {
            await blockedNotifier.NotifyAsync(AttachmentBlockedKind.ScanFailed, attachment, ct);
        }
    }

    // Transient = "the scanner could not be reached or didn't reply in
    // time." Things like a malformed protocol response are not retried
    // (separate catch in the retry loop) since they indicate a bug.
    private static bool IsTransientScannerFailure(Exception ex) =>
        ex is SocketException
            or IOException
            or TimeoutException;
}
