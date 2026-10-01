using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.StartLivestream;

public sealed class StartLivestreamCommandValidator : AbstractValidator<StartLivestreamCommand>
{
    public StartLivestreamCommandValidator()
    {
        RuleFor(x => x.LivestreamId)
            .NotEmpty()
            .WithMessage("LivestreamId không hợp lệ.");
    }
}
