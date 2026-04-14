using EduConnect.Api.Features.Attachments;
using FluentValidation;

namespace EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

public class GetAttachmentsForEntityQueryValidator : AbstractValidator<GetAttachmentsForEntityQuery>
{
    public GetAttachmentsForEntityQueryValidator()
    {
        RuleFor(x => x.EntityId)
            .NotEmpty().WithMessage("Entity ID is required.");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(entityType => AttachmentFeatureRules.SupportedEntityTypes.Contains(entityType))
            .WithMessage("Entity type must be 'homework' or 'notice'.");
    }
}
