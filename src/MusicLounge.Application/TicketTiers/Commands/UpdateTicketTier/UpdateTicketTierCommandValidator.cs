using FluentValidation;

namespace MusicLounge.Application.TicketTiers.Commands.UpdateTicketTier;

public sealed class UpdateTicketTierCommandValidator : AbstractValidator<UpdateTicketTierCommand>
{
    public UpdateTicketTierCommandValidator()
    {
        RuleFor(x => x.TierId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TotalCapacity)
            .GreaterThan(0)
            .When(x => x.TotalCapacity.HasValue)
            .WithMessage("TotalCapacity phải lớn hơn 0.");
    }
}
