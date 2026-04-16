using FluentValidation;

namespace EduConnect.Api.Features.Attendance.RejectLeave;

public class RejectLeaveCommandValidator : AbstractValidator<RejectLeaveCommand>
{
    public RejectLeaveCommandValidator()
    {
        RuleFor(x => x.LeaveApplicationId)
            .NotEmpty().WithMessage("Leave application ID is required.");

        RuleFor(x => x.ReviewNote)
            .NotEmpty().WithMessage("Rejection note is required.")
            .MaximumLength(1000).WithMessage("Rejection note cannot exceed 1000 characters.");
    }
}

