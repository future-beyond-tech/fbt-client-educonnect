using FluentValidation;

namespace EduConnect.Api.Features.Attachments.AttachFileToEntity;

public class AttachFileToEntityCommandValidator : AbstractValidator<AttachFileToEntityCommand>
{
    private static readonly string[] AllowedEntityTypes = { "homework", "notice" };

    public AttachFileToEntityCommandValidator()
    {
        RuleFor(x => x.AttachmentId)
            .NotEmpty().WithMessage("Attachment ID is required.");

        RuleFor(x => x.EntityId)
            .NotEmpty().WithMessage("Entity ID is required.");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(et => AllowedEntityTypes.Contains(et))
            .WithMessage("Entity type must be 'homework' or 'notice'.");
    }
}
