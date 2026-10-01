using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.CancelHold;

public sealed class CancelHoldCommandValidator : AbstractValidator<CancelHoldCommand>
{
    public CancelHoldCommandValidator()
        => RuleFor(x => x.HoldId).NotEmpty().WithMessage("HoldId không hợp lệ.");
}
