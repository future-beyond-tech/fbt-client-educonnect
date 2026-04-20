using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;

namespace EduConnect.Api.Features.Push.UnregisterPushSubscription;

/// <summary>
/// Removes a push subscription by endpoint. Called when the user signs out
/// on a device, disables notifications, or revokes permission.
/// </summary>
public class UnregisterPushSubscriptionCommandHandler
    : IRequestHandler<UnregisterPushSubscriptionCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<UnregisterPushSubscriptionCommandHandler> _logger;

    public UnregisterPushSubscriptionCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<UnregisterPushSubscriptionCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        UnregisterPushSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            throw new UnauthorizedException("Authentication required.");
        }

        var deleted = await _context.UserPushSubscriptions
            .Where(s => s.Endpoint == request.Endpoint &&
                        s.UserId == _currentUserService.UserId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Unregistered push subscription for user {UserId} (endpoint hash={EndpointHash})",
                _currentUserService.UserId,
                request.Endpoint.GetHashCode());
        }

        return Unit.Value;
    }
}
