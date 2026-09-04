using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.UpdateSeatingZone;

public sealed class UpdateSeatingZoneCommandValidator : AbstractValidator<UpdateSeatingZoneCommand>
{
    public UpdateSeatingZoneCommandValidator()
    {
        RuleFor(x => x.ZoneId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Capacity).GreaterThan(0);
    }
}
