using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;

public sealed class SetPlaybackModeCommandValidator : AbstractValidator<SetPlaybackModeCommand>
{
    private static readonly string[] ValidModes = ["TwoD", "ThreeD"];

    public SetPlaybackModeCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.PlaybackMode)
            .Must(m => ValidModes.Contains(m))
            .WithMessage("PlaybackMode phải là 'TwoD' hoặc 'ThreeD'.");
    }
}
