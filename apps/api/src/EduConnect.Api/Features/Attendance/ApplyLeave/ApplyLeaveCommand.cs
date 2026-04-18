namespace EduConnect.Api.Features.Attendance.ApplyLeave;

/// <summary>
/// Apply for leave for one or more children.
///
/// A parent with multiple linked children can submit a single request covering
/// any subset of those children — the backend creates one
/// <see cref="Infrastructure.Database.Entities.LeaveApplicationEntity"/> per
/// selected student in a single transaction.
///
/// Backward compatibility: legacy callers may send a single <c>StudentId</c>
/// instead of <c>StudentIds</c>. The handler normalizes both into a single
/// canonical set before processing.
/// </summary>
public record ApplyLeaveCommand(
    Guid[] StudentIds,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    // Legacy single-child field — accepted from older clients. Prefer StudentIds.
    Guid? StudentId = null
) : IRequest<ApplyLeaveResponse>;

/// <summary>
/// Result of an apply-leave submission.
///
/// <see cref="LeaveApplicationId"/> is retained for backward compatibility with
/// single-child callers — it carries the first created ID. New clients should
/// read <see cref="LeaveApplicationIds"/> for the full list.
/// </summary>
public record ApplyLeaveResponse(
    Guid LeaveApplicationId,
    Guid[] LeaveApplicationIds,
    int CreatedCount,
    string Status,
    string Message);
