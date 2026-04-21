using System.Threading.Channels;

namespace EduConnect.Api.Infrastructure.Services.Scanning;

/// <summary>
/// In-memory queue of attachment IDs awaiting virus scan. Single-instance
/// only: if we ever horizontally scale the API, this needs to move to a
/// durable queue (Redis/PG LISTEN). The Channel is bounded so a burst of
/// uploads can't blow up heap — backpressure falls on the enqueuer.
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
