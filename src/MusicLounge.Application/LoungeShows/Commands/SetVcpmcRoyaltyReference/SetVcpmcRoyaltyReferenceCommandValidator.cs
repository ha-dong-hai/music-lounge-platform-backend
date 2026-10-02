using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetVcpmcRoyaltyReference;

public sealed class SetVcpmcRoyaltyReferenceCommandValidator : AbstractValidator<SetVcpmcRoyaltyReferenceCommand>
{
    public SetVcpmcRoyaltyReferenceCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();
        RuleFor(x => x.VcpmcRoyaltyReference).NotEmpty().MaximumLength(500);
    }
}
