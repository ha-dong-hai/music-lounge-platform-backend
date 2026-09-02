using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Moderations.Commands.ResolveContentReport;

internal sealed class ResolveContentReportCommandValidator : AbstractValidator<ResolveContentReportCommand>
{
    public ResolveContentReportCommandValidator()
    {
        RuleFor(x => x.TargetType)
            .Must(t => Enum.TryParse<ReportTargetType>(t, true, out _))
            .WithMessage($"TargetType phải là một trong: {string.Join(", ", Enum.GetNames<ReportTargetType>())}.");

        RuleFor(x => x.TargetId).GreaterThan(0);

        RuleFor(x => x.Action)
            .Must(a => a.Equals("Removed", StringComparison.OrdinalIgnoreCase)
                || a.Equals("Dismissed", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Action phải là 'Removed' hoặc 'Dismissed'.");

        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
