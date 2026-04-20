using System.Net;
using System.Text.Json;
using EduConnect.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace EduConnect.Api.Infrastructure.Services;

/// <summary>
/// Sends Web Push notifications via the standard Web Push protocol.
/// Works with Chrome/Edge (FCM), Firefox (Mozilla autopush), and Safari 16.4+
/// (Apple Push Notification service) using one set of VAPID keys.
/// </summary>
public class WebPushSender : IPushSender
{
    private readonly AppDbContext _context;
    private readonly WebPushOptions _options;
    private readonly ILogger<WebPushSender> _logger;
    private readonly WebPushClient _client;

    public WebPushSender(
        AppDbContext context,
        IOptions<WebPushOptions> options,
        ILogger<WebPushSender> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
        _client = new WebPushClient();
    }

    public async Task FanOutAsync(
        IReadOnlyList<Guid> userIds,
        PushPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.PublicKey) ||
            string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            _logger.LogDebug("WebPush is disabled or missing VAPID keys — skipping fan-out.");
            return;
        }

        if (userIds.Count == 0)
        {
            return;
        }

        var distinctIds = userIds.Distinct().ToList();

        // IgnoreQueryFilters because the sender may run outside a user scope
        // (e.g. background operations, batch sends) and we only filter by
        // explicit UserIds we were given.
        var subscriptions = await _context.UserPushSubscriptions
            .IgnoreQueryFilters()
            .Where(s => distinctIds.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        var vapid = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var body = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            url = payload.Url,
            type = payload.Type,
            entityId = payload.EntityId,
            entityType = payload.EntityType,
        });

        var staleIds = new List<Guid>();
        var now = DateTimeOffset.UtcNow;

        foreach (var subscription in subscriptions)
        {
            try
            {
                var pushSubscription = new PushSubscription(
                    subscription.Endpoint,
                    subscription.P256dh,
                    subscription.Auth);

                await _client.SendNotificationAsync(pushSubscription, body, vapid, cancellationToken);
                subscription.LastUsedAt = now;
            }
            catch (WebPushException ex) when (
                ex.StatusCode == HttpStatusCode.Gone ||
                ex.StatusCode == HttpStatusCode.NotFound)
            {
                // 404/410 = browser revoked the subscription. Remove it.
                staleIds.Add(subscription.Id);
                _logger.LogInformation(
                    "Removing stale push subscription {SubscriptionId} for user {UserId} (status {Status})",
                    subscription.Id, subscription.UserId, (int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send push to subscription {SubscriptionId} for user {UserId}",
                    subscription.Id, subscription.UserId);
            }
        }

        // Persist LastUsedAt updates on surviving rows first, then drop stale
        // ones. Order matters: ExecuteDeleteAsync bypasses the change tracker.
        await _context.SaveChangesAsync(cancellationToken);

        if (staleIds.Count > 0)
        {
            await _context.UserPushSubscriptions
                .IgnoreQueryFilters()
                .Where(s => staleIds.Contains(s.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Null implementation used when push is disabled or VAPID keys are absent.
/// Lets the rest of the app run without conditional checks.
/// </summary>
public class NullPushSender : IPushSender
{
    public Task FanOutAsync(
        IReadOnlyList<Guid> userIds,
        PushPayload payload,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
