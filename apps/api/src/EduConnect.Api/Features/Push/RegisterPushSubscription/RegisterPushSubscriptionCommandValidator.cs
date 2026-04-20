using FluentValidation;

namespace EduConnect.Api.Features.Push.RegisterPushSubscription;

public class RegisterPushSubscriptionCommandValidator : AbstractValidator<RegisterPushSubscriptionCommand>
{
    public RegisterPushSubscriptionCommandValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint is required.")
            .MaximumLength(2048)
            .Must(v => Uri.TryCreate(v, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .WithMessage("Endpoint must be an absolute https URL.");

        RuleFor(x => x.P256dh)
            .NotEmpty().WithMessage("p256dh key is required.")
            .MaximumLength(256);

        RuleFor(x => x.Auth)
            .NotEmpty().WithMessage("Auth secret is required.")
            .MaximumLength(64);

        RuleFor(x => x.UserAgent)
            .MaximumLength(512);
    }
}
