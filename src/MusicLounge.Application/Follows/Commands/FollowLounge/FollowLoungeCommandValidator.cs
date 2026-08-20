using FluentValidation;

namespace MusicLounge.Application.Follows.Commands.FollowLounge;

public sealed class FollowLoungeCommandValidator : AbstractValidator<FollowLoungeCommand>
{
    public FollowLoungeCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0).WithMessage("LoungeId không hợp lệ.");
    }
}
