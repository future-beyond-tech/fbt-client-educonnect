using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// Recovers attachment scan jobs that were lost across a restart. The
/// in-memory queue (<see cref="ChannelAttachmentScanQueue"/>) is volatile,
/// so any row that was enqueued but not scanned before shutdown sits in
/// the database with <c>Status = Pending</c> and never progresses unless
/// something re-enqueues it.
///
/// Runs once on host startup, before <see cref="AttachmentScanWorker"/>
/// begins draining new uploads. Scoped DbContext so we don't hold one
/// open for the lifetime of the host. RLS-bypass via
/// <c>IgnoreQueryFilters</c> for the same reason as the worker — there
/// is no tenant context in a startup hook.
///
/// This is the lightweight alternative to a transactional outbox table:
/// it accepts a small recovery window (the grace period) instead of
/// closing the commit-vs-enqueue race entirely. If observed reconciler
/// counts grow beyond a handful per restart, escalate to a real outbox.
/// </summary>
public sealed class AttachmentScanReconciler : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAttachmentScanQueue _queue;
    private readonly AttachmentScannerOptions _options;
    private readonly ILogger<AttachmentScanReconciler> _logger;

    public AttachmentScanReconciler(
        IServiceScopeFactory scopeFactory,
        IAttachmentScanQueue queue,
        IOptions<AttachmentScannerOptions> options,
        ILogger<AttachmentScanReconciler> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await ReconcileAsync(cancellationToken);
            _logger.LogInformation(
                "AttachmentScanReconciler completed: {Count} stale Pending attachment(s) re-enqueued",
                count);
        }
        catch (Exception ex)
        {
            // Reconciler failure must not block host startup. The worker
            // will still drain new uploads; an operator can re-trigger
            // reconciliation later (or restart again).
            _logger.LogError(ex,
                "AttachmentScanReconciler failed during startup; continuing without recovered jobs");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var graceMinutes = Math.Max(0, _options.ReconciliationGraceMinutes);
        var threshold = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(graceMinutes);

        var staleIds = await db.Attachments
            .IgnoreQueryFilters()
            .Where(a => a.Status == AttachmentStatus.Pending && a.UploadedAt < threshold)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in staleIds)
        {
            await _queue.EnqueueAsync(id, cancellationToken);
        }

        return staleIds.Count;
    }
}
