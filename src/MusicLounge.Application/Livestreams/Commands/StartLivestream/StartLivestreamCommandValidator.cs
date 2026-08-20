using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.StartLivestream;

public sealed class StartLivestreamCommandValidator : AbstractValidator<StartLivestreamCommand>
{
    public StartLivestreamCommandValidator()
    {
        RuleFor(x => x.LivestreamId)
            .GreaterThan(0)
            .WithMessage("LivestreamId không hợp lệ.");
    }
}
