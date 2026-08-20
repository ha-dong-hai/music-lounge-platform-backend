using FluentValidation;

namespace MusicLounge.Application.Staffing.Commands.DeactivateStaff;

public sealed class DeactivateStaffCommandValidator : AbstractValidator<DeactivateStaffCommand>
{
    public DeactivateStaffCommandValidator()
    {
        RuleFor(x => x.LoungeStaffId).GreaterThan(0);
    }
}
