using FluentValidation;

namespace MusicLounge.Application.Performers.Commands.RemovePerformerSocialLink;

public sealed class RemovePerformerSocialLinkCommandValidator : AbstractValidator<RemovePerformerSocialLinkCommand>
{
    public RemovePerformerSocialLinkCommandValidator()
    {
        RuleFor(x => x.PerformerId).NotEmpty();
        RuleFor(x => x.LinkId).NotEmpty();
    }
}
