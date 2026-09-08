using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.UpdatePerformance;

public sealed class UpdatePerformanceCommandValidator : AbstractValidator<UpdatePerformanceCommand>
{
    public UpdatePerformanceCommandValidator()
    {
        RuleFor(x => x.PerformanceId).GreaterThan(0);
        RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<PerformerRole>(r, ignoreCase: true, out _))
            .WithMessage($"Role phải là một trong: {string.Join(", ", Enum.GetNames<PerformerRole>())}.");
        RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
    }
}
