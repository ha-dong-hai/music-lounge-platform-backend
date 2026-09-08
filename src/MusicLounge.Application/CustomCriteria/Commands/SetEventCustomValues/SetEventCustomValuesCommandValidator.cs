using FluentValidation;

namespace MusicLounge.Application.CustomCriteria.Commands.SetEventCustomValues;

internal sealed class SetEventCustomValuesCommandValidator : AbstractValidator<SetEventCustomValuesCommand>
{
    public SetEventCustomValuesCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);

        RuleForEach(x => x.Values).ChildRules(v =>
        {
            v.RuleFor(x => x.CriteriaId).GreaterThan(0);
            v.RuleFor(x => x.Value).NotEmpty().MaximumLength(1000);
        });
    }
}
