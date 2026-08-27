using FluentValidation;

namespace MusicLounge.Application.Livestreams.Commands.SetChatEnabled;

public sealed class SetChatEnabledCommandValidator : AbstractValidator<SetChatEnabledCommand>
{
    public SetChatEnabledCommandValidator()
    {
        RuleFor(x => x.LivestreamId).GreaterThan(0).WithMessage("LivestreamId không hợp lệ.");
    }
}
