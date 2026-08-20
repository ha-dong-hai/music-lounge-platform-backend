using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.CreateSeatingZone;

public sealed class CreateSeatingZoneCommandValidator : AbstractValidator<CreateSeatingZoneCommand>
{
    public CreateSeatingZoneCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Capacity).GreaterThan(0);
    }
}
