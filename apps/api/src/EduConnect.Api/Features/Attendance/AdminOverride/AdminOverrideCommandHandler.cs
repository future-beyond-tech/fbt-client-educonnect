using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using MediatR;

namespace EduConnect.Api.Features.Attendance.AdminOverride;

public class AdminOverrideCommandHandler : IRequestHandler<AdminOverrideCommand, AdminOverrideResponse>
{
    private readonly AppDbContext _context;
    private readonly CurrentUserService _currentUserService;
    private readonly ILogger<AdminOverrideCommandHandler> _logger;

    public AdminOverrideCommandHandler(
        AppDbContext context,
        CurrentUserService currentUserService,
        ILogger<AdminOverrideCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<AdminOverrideResponse> Handle(AdminOverrideCommand request, CancellationToken cancellationToken)
    {
        if (_currentUserService.Role != "Admin")
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted admin override",
                _currentUserService.UserId, _currentUserService.Role);
            throw new ForbiddenException("Only administrators can perform this action.");
        }

        var record = await _context.AttendanceRecords
            .FirstOrDefaultAsync(ar =>
                ar.Id == request.RecordId &&
                ar.SchoolId == _currentUserService.SchoolId,
                cancellationToken);

        if (record == null)
        {
            _logger.LogWarning("Attendance record {RecordId} not found", request.RecordId);
            throw new NotFoundException("Attendance", request.RecordId.ToString());
        }

        record.IsDeleted = true;
        record.DeletedAt = DateTimeOffset.UtcNow;

        var newRecord = new AttendanceRecordEntity
        {
            Id = Guid.NewGuid(),
            SchoolId = _currentUserService.SchoolId,
            StudentId = record.StudentId,
            Date = record.Date,
            Status = record.Status,
            Reason = $"[OVERRIDE] {request.Reason}",
            EnteredById = _currentUserService.UserId,
            EnteredByRole = _currentUserService.Role,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.AttendanceRecords.Add(newRecord);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Attendance record {OldRecordId} overridden with new record {NewRecordId} by admin {AdminId}. Reason: {Reason}",
            request.RecordId, newRecord.Id, _currentUserService.UserId, request.Reason);

        return new AdminOverrideResponse(newRecord.Id, "Attendance record overridden successfully.");
    }
}
