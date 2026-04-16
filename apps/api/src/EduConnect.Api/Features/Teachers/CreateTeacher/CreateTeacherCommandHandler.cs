using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.PhoneNumbers;
using EduConnect.Api.Features.Teachers.AssignClassToTeacher;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Teachers.CreateTeacher;

public class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, CreateTeacherResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly PasswordHasher _passwordHasher;
    private readonly ISender _sender;
    private readonly ILogger<CreateTeacherCommandHandler> _logger;

    public CreateTeacherCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        PasswordHasher passwordHasher,
        ISender sender,
        ILogger<CreateTeacherCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _sender = sender;
        _logger = logger;
    }

    public async Task<CreateTeacherResponse> Handle(CreateTeacherCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            throw new ForbiddenException("Only admins can create staff accounts.");
        }

        var trimmedName = request.Name.Trim();
        var normalizedPhone = JapanPhoneNumber.NormalizeUserInput(request.Phone);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedRole = NormalizeRole(request.Role);

        var phoneExists = await _context.Users
            .AnyAsync(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Phone == normalizedPhone,
                cancellationToken);

        if (phoneExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Phone", new[] { "A user with this phone number already exists." } }
            });
        }

        var emailExists = await _context.Users
            .AnyAsync(u =>
                u.SchoolId == _currentUserService.SchoolId &&
                u.Email != null &&
                u.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (emailExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { "Email", new[] { "A user with this email already exists." } }
            });
        }

        var teacher = new UserEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            Name = trimmedName,
            Phone = normalizedPhone,
            Email = normalizedEmail,
            Role = normalizedRole,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Users.Add(teacher);

        var hasInitialAssignment = normalizedRole == "Teacher" &&
            request.ClassId.HasValue &&
            !string.IsNullOrWhiteSpace(request.Subject);

        if (hasInitialAssignment)
        {
            var trimmedSubject = request.Subject!.Trim();
            if (_context.Database.IsRelational())
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    await _sender.Send(
                        new AssignClassToTeacherCommand(
                            teacher.Id,
                            request.ClassId!.Value,
                            trimmedSubject,
                            request.IsClassTeacher),
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            else
            {
                // EF InMemory (tests) does not support relational transactions.
                await _context.SaveChangesAsync(cancellationToken);
                await _sender.Send(
                    new AssignClassToTeacherCommand(
                        teacher.Id,
                        request.ClassId!.Value,
                        trimmedSubject,
                        request.IsClassTeacher),
                    cancellationToken);
            }
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "{Role} account {TeacherId} created by admin {AdminId}",
            normalizedRole,
            teacher.Id,
            _currentUserService.UserId);

        return new CreateTeacherResponse(teacher.Id, $"{normalizedRole} created successfully.");
    }

    private static string NormalizeRole(string role)
    {
        return string.Equals(role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : "Teacher";
    }
}
