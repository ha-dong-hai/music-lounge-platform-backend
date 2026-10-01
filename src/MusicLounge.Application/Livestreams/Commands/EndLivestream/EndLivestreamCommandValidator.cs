using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.EndLivestream;

public sealed class EndLivestreamCommandValidator : AbstractValidator<EndLivestreamCommand>
{
    public EndLivestreamCommandValidator()
    {
        RuleFor(x => x.LivestreamId)
            .NotEmpty()
            .WithMessage("LivestreamId không hợp lệ.");
    }
}
