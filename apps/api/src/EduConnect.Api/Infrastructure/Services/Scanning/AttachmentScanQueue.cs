using System.Threading.Channels;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// In-memory queue of attachment IDs awaiting virus scan. Single-instance
/// only: if we ever horizontally scale the API, this needs to move to a
/// durable queue (Redis/PG LISTEN). The Channel is bounded so a burst of
/// uploads can't blow up heap — backpressure falls on the enqueuer.
///
/// Durability model — accepted design as of Phase 3 remediation
/// (2026-04-22):
///   This queue is volatile. The attach handler commits the
///   attachment row with Status=Pending and then enqueues the ID in a
///   second step (no transactional outbox). A crash between those two
///   operations leaves a Pending row with no queued job.
///
///   Recovery path: <see cref="AttachmentScanReconciler"/> runs at
///   host startup and re-enqueues every Pending row older than the
///   configured grace window
///   (<see cref="AttachmentScannerOptions.ReconciliationGraceMinutes"/>,
///   default 2 min). The worker's "skip non-Pending" guard makes the
///   re-enqueue idempotent against still-running attach calls.
///
///   This is the lightweight alternative to a real outbox table. We
///   accept a recovery window in exchange for not maintaining a
///   second persisted queue. If reconciliation counts ever grow past
///   a handful of rows per restart, escalate by introducing a
///   transactional outbox (commit ID into outbox in the same
///   transaction as the attachment row, separate dispatcher process
///   reads-and-acks).
/// </summary>
public interface IAttachmentScanQueue
{
    ValueTask EnqueueAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}

public sealed class ChannelAttachmentScanQueue : IAttachmentScanQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(capacity: 256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(Guid attachmentId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(attachmentId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
