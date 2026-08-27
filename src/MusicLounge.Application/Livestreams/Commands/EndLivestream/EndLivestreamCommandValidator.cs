using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.EndLivestream;

public sealed class EndLivestreamCommandValidator : AbstractValidator<EndLivestreamCommand>
{
    public EndLivestreamCommandValidator()
    {
        RuleFor(x => x.LivestreamId)
            .GreaterThan(0)
            .WithMessage("LivestreamId không hợp lệ.");
    }
}
