using FluentValidation;

namespace MusicLounge.Application.Follows.Commands.UnfollowLounge;

public sealed class UnfollowLoungeCommandValidator : AbstractValidator<UnfollowLoungeCommand>
{
    public UnfollowLoungeCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0).WithMessage("LoungeId không hợp lệ.");
    }
}
