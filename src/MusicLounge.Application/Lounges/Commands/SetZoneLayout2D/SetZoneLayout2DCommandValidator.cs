using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetZoneLayout2D;

public sealed class SetZoneLayout2DCommandValidator : AbstractValidator<SetZoneLayout2DCommand>
{
    public SetZoneLayout2DCommandValidator()
    {
        RuleFor(x => x.ZoneId).GreaterThan(0);

        // He toa do % (0-100) tren canvas so do — doc lap do phan giai man hinh.
        RuleFor(x => x.X).InclusiveBetween(0, 100);
        RuleFor(x => x.Y).InclusiveBetween(0, 100);
        RuleFor(x => x.Width).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Height).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.RotationDeg).InclusiveBetween(-360, 360);

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")
            .When(x => x.Color is not null)
            .WithMessage("Màu phải ở định dạng hex (#RRGGBB hoặc #RRGGBBAA).");
    }
}
