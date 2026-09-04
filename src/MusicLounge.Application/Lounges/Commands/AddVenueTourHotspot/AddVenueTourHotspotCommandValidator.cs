using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourHotspot;

public sealed class AddVenueTourHotspotCommandValidator : AbstractValidator<AddVenueTourHotspotCommand>
{
    public AddVenueTourHotspotCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.SceneId).GreaterThan(0);
        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<VenueTourHotspotType>(t, ignoreCase: true, out _))
            .WithMessage("Type phải là 'Navigate' hoặc 'Info'.");
        RuleFor(x => x.Yaw).InclusiveBetween(-180, 180);
        RuleFor(x => x.Pitch).InclusiveBetween(-90, 90);
        RuleFor(x => x.Label).MaximumLength(100);
        RuleFor(x => x.InfoText).MaximumLength(2000);

        RuleFor(x => x.TargetSceneId)
            .NotNull().WithMessage("Hotspot loại Navigate phải có TargetSceneId.")
            .When(x => string.Equals(x.Type, nameof(VenueTourHotspotType.Navigate), StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.TargetSceneId)
            .Must((cmd, targetSceneId) => targetSceneId != cmd.SceneId)
            .WithMessage("Hotspot không thể trỏ về chính scene đang chứa nó.")
            .When(x => x.TargetSceneId.HasValue);
    }
}
