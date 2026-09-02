using FluentValidation;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Moderations.Commands.SubmitContentReport;

internal sealed class SubmitContentReportCommandValidator : AbstractValidator<SubmitContentReportCommand>
{
    public SubmitContentReportCommandValidator()
    {
        RuleFor(x => x.TargetType)
            .Must(t => Enum.TryParse<ReportTargetType>(t, true, out _))
            .WithMessage($"TargetType phải là một trong: {string.Join(", ", Enum.GetNames<ReportTargetType>())}.");

        RuleFor(x => x.TargetId).GreaterThan(0);

        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
