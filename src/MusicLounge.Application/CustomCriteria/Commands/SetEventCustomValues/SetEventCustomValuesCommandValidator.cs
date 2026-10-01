using FluentValidation;

namespace MusicLounge.Application.CustomCriteria.Commands.SetEventCustomValues;

internal sealed class SetEventCustomValuesCommandValidator : AbstractValidator<SetEventCustomValuesCommand>
{
    public SetEventCustomValuesCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();

        RuleForEach(x => x.Values).ChildRules(v =>
        {
            v.RuleFor(x => x.CriteriaId).NotEmpty();
            v.RuleFor(x => x.Value).NotEmpty().MaximumLength(1000);
        });
    }
}
