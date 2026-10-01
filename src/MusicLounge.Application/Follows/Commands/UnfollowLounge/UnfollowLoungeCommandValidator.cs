using FluentValidation;

namespace MusicLounge.Application.Follows.Commands.UnfollowLounge;

public sealed class UnfollowLoungeCommandValidator : AbstractValidator<UnfollowLoungeCommand>
{
    public UnfollowLoungeCommandValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty().WithMessage("LoungeId không hợp lệ.");
    }
}
