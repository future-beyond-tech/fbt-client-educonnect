using FluentValidation;

namespace EduConnect.Api.Features.Attendance.UpdateLeaveApplication;

public class UpdateLeaveApplicationCommandValidator : AbstractValidator<UpdateLeaveApplicationCommand>
{
    public UpdateLeaveApplicationCommandValidator()
    {
        RuleFor(x => x.LeaveApplicationId)
            .NotEmpty().WithMessage("Leave application ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date cannot be before start date.");
    }
}

