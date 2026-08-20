using FluentValidation;

namespace MusicLounge.Application.Performers.Commands.RemovePerformerSocialLink;

public sealed class RemovePerformerSocialLinkCommandValidator : AbstractValidator<RemovePerformerSocialLinkCommand>
{
    public RemovePerformerSocialLinkCommandValidator()
    {
        RuleFor(x => x.PerformerId).GreaterThan(0);
        RuleFor(x => x.LinkId).GreaterThan(0);
    }
}
