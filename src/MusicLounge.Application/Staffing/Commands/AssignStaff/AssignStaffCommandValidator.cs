using FluentValidation;

namespace MusicLounge.Application.Staffing.Commands.AssignStaff;

public sealed class AssignStaffCommandValidator : AbstractValidator<AssignStaffCommand>
{
    public AssignStaffCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
