using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services.Scanning;

namespace EduConnect.Api.Tests;

/// <summary>
/// Test stub: ignores every notification call. Used by worker tests that
/// don't care about the notifier side-channel and just want a registered
/// instance so DI doesn't throw.
/// </summary>
internal sealed class NoOpAttachmentBlockedNotifier : IAttachmentBlockedNotifier
{
    public static readonly NoOpAttachmentBlockedNotifier Instance = new();

    public Task NotifyAsync(
        AttachmentBlockedKind kind,
        AttachmentEntity attachment,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
