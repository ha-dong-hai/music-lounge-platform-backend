using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Performers.Commands.AddPerformerSocialLink;

public sealed class AddPerformerSocialLinkCommandValidator : AbstractValidator<AddPerformerSocialLinkCommand>
{
    public AddPerformerSocialLinkCommandValidator()
    {
        RuleFor(x => x.PerformerId).GreaterThan(0);

        RuleFor(x => x.Platform)
            .Must(p => Enum.TryParse<SocialPlatform>(p, ignoreCase: true, out _))
            .WithMessage($"Platform phải là một trong: {string.Join(", ", Enum.GetNames<SocialPlatform>())}.");

        RuleFor(x => x.Url)
            .NotEmpty().MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Url phải là một đường dẫn http/https hợp lệ.");

        RuleFor(x => x.DisplayName).MaximumLength(255);
    }
}
