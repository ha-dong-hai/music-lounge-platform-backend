using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.AddPerformance;

public sealed class AddPerformanceCommandValidator : AbstractValidator<AddPerformanceCommand>
{
    public AddPerformanceCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();
        RuleFor(x => x.PerformerId).NotEmpty().When(x => x.PerformerId.HasValue);
        RuleFor(x => x.PerformerName).MaximumLength(255).When(x => x.PerformerName is not null);
        RuleFor(x => x)
            .Must(x => x.PerformerId.HasValue || !string.IsNullOrWhiteSpace(x.PerformerName))
            .WithMessage("Phải cung cấp PerformerId hoặc PerformerName.");
        RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<PerformerRole>(r, ignoreCase: true, out _))
            .WithMessage($"Role phải là một trong: {string.Join(", ", Enum.GetNames<PerformerRole>())}.");
        RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
    }
}
