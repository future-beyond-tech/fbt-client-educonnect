using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Periodic janitor that reconciles attachment rows whose backing storage
/// object has gone away. Picks up dangling references that the
/// happy-path delete missed (S3 delete succeeded, DB delete then failed):
/// the row keeps pointing at a key whose object no longer exists, so the
/// next download attempt would 404. Once detected, the row is removed
/// outright — the storage object the row promised has already been
/// gone, so "complete the operation that almost finished" is the right
/// move.
///
/// Runs once at startup, then every <see cref="IntervalHours"/>.
/// Skipping the very-recent rows by <see cref="MinAgeForReconciliation"/>
/// avoids racing with attach-time uploads where the HEAD check was
/// transiently inconsistent.
/// </summary>
public sealed class AttachmentStorageReconciler : BackgroundService
{
    public static readonly TimeSpan IntervalHours = TimeSpan.FromHours(24);
    public static readonly TimeSpan MinAgeForReconciliation = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AttachmentStorageReconciler> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _minAge;

    public AttachmentStorageReconciler(
        IServiceScopeFactory scopeFactory,
        ILogger<AttachmentStorageReconciler> logger)
        : this(scopeFactory, logger, IntervalHours, MinAgeForReconciliation)
    {
    }

    // Test-only ctor lets the suite collapse the interval and grace
    // window without sitting through a 24-hour delay.
    internal AttachmentStorageReconciler(
        IServiceScopeFactory scopeFactory,
        ILogger<AttachmentStorageReconciler> logger,
        TimeSpan interval,
        TimeSpan minAge)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = interval;
        _minAge = minAge;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AttachmentStorageReconciler started");

        // Run once on boot, then on the configured cadence. Failures don't
        // crash the loop — log and try again at the next interval.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = await ReconcileOnceAsync(stoppingToken);
                if (removed > 0)
                {
                    _logger.LogInformation(
                        "AttachmentStorageReconciler removed {Count} dangling attachment row(s)",
                        removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AttachmentStorageReconciler iteration failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task<int> ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        var threshold = DateTimeOffset.UtcNow - _minAge;

        // Only consider rows whose status implies "the object should still
        // be there." Infected rows have already been deleted by the worker
        // by design and don't qualify.
        var candidates = await db.Attachments
            .IgnoreQueryFilters()
            .Where(a => a.Status != AttachmentStatus.Infected && a.UploadedAt < threshold)
            .Select(a => new { a.Id, a.StorageKey })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = await storage.GetObjectMetadataAsync(candidate.StorageKey, cancellationToken);
            if (metadata is not null)
            {
                continue; // object exists, nothing to do
            }

            // Storage says 404. Complete the delete the original caller
            // never finished. We re-load and Remove via EF so the xmin
            // concurrency token catches a concurrent writer (rare).
            try
            {
                var row = await db.Attachments
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a => a.Id == candidate.Id, cancellationToken);
                if (row is null) continue;

                db.Attachments.Remove(row);
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Dangling attachment row removed: {AttachmentId} (storage 404 for {Key})",
                    candidate.Id, candidate.StorageKey);

                removed++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogInformation(ex,
                    "Concurrent writer raced AttachmentStorageReconciler for {AttachmentId}; skipping",
                    candidate.Id);
            }
        }

        return removed;
    }
}
