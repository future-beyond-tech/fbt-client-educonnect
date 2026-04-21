using FluentValidation;

namespace EduConnect.Api.Common.Auth;

// Single source of truth for password rules. Every entry point where a user
// sets a NEW password (register, change, reset, admin-create) must route
// through PasswordPolicyValidator so the bar is enforced identically.
//
// Login does NOT use this — users with legacy short passwords must still be
// able to sign in so they can be rotated on next login.
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    // Cutoff for legacy-password rotation. Users whose password was last
    // updated BEFORE this instant are forced to change on next login.
    // Bump this value only when introducing a stricter policy that should
    // flush everyone through the change-password flow again.
    public static readonly DateTimeOffset PolicyEnforcedAt =
        new(2026, 4, 22, 0, 0, 0, TimeSpan.Zero);

    public static bool IsLegacyPassword(DateTimeOffset? passwordUpdatedAt)
        => passwordUpdatedAt is null || passwordUpdatedAt.Value < PolicyEnforcedAt;
}

public sealed class PasswordPolicyValidator : AbstractValidator<string>
{
    public PasswordPolicyValidator()
    {
        RuleFor(p => p)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(PasswordPolicy.MinLength)
                .WithMessage($"Password must be at least {PasswordPolicy.MinLength} characters.")
            .MaximumLength(PasswordPolicy.MaxLength)
                .WithMessage($"Password must be {PasswordPolicy.MaxLength} characters or fewer.")
            .Matches(@"[A-Za-z]").WithMessage("Password must contain at least one letter.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.");
    }
}
