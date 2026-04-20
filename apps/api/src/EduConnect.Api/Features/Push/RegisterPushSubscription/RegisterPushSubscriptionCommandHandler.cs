using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;

namespace EduConnect.Api.Features.Push.RegisterPushSubscription;

/// <summary>
/// Upsert a browser push subscription for the current user. The push-service
/// endpoint URL is globally unique per device/browser-profile, so we treat it
/// as the idempotency key: re-subscribing from the same browser rebinds the
/// existing row to the current user instead of creating duplicates.
/// </summary>
public class RegisterPushSubscriptionCommandHandler
    : IRequestHandler<RegisterPushSubscriptionCommand, RegisterPushSubscriptionResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<RegisterPushSubscriptionCommandHandler> _logger;

    public RegisterPushSubscriptionCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<RegisterPushSubscriptionCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<RegisterPushSubscriptionResponse> Handle(
        RegisterPushSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new UnauthorizedException("Authentication required.");
        }

        var existing = await _context.UserPushSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);

        if (existing != null)
        {
            existing.UserId = _currentUserService.UserId;
            existing.SchoolId = _currentUserService.SchoolId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.UserAgent = request.UserAgent;
            existing.LastUsedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated push subscription {SubscriptionId} for user {UserId}",
                existing.Id, existing.UserId);
            return new RegisterPushSubscriptionResponse(existing.Id);
        }

        var entity = new UserPushSubscriptionEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            UserId = _currentUserService.UserId,
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth,
            UserAgent = request.UserAgent,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.UserPushSubscriptions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered push subscription {SubscriptionId} for user {UserId}",
            entity.Id, entity.UserId);

        return new RegisterPushSubscriptionResponse(entity.Id);
    }
}
