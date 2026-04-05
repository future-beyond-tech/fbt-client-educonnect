using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using MediatR;

namespace EduConnect.Api.Features.Auth.SetPin;

public class SetPinCommandHandler : IRequestHandler<SetPinCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly PinService _pinService;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<SetPinCommandHandler> _logger;

    public SetPinCommandHandler(
        AppDbContext context,
        PinService pinService,
        CurrentUserService currentUserService,
        ILogger<SetPinCommandHandler> logger)
    {
        _context = context;
        _pinService = pinService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Unit> Handle(SetPinCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Parent")
        {
            _logger.LogWarning("Non-parent user {UserId} attempted to set PIN", _currentUserService.UserId);
            throw new ForbiddenException("Only parents can set PIN.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId && u.IsActive, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found while attempting to set PIN", _currentUserService.UserId);
            throw new NotFoundException("User not found.");
        }

        if (!_pinService.ValidatePinFormat(request.Pin))
        {
            _logger.LogWarning("Invalid PIN format attempt by user {UserId}", _currentUserService.UserId);
            throw new ValidationException(
                new Dictionary<string, string[]>
                {
                    { "pin", new[] { "PIN must be 4-6 digits." } }
                });
        }

        var pinHash = _pinService.HashPin(request.Pin);
        user.PinHash = pinHash;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIN set successfully for parent user {UserId}", user.Id);

        return Unit.Value;
    }
}
