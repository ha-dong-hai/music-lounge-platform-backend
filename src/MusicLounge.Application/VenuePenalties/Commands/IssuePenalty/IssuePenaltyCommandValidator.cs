using FluentValidation;

namespace MusicLounge.Application.VenuePenalties.Commands.IssuePenalty;

public sealed class IssuePenaltyCommandValidator : AbstractValidator<IssuePenaltyCommand>
{
    private static readonly string[] ValidTypes = ["Warning", "Suspension", "Ban"];

    public IssuePenaltyCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0).WithMessage("LoungeId không hợp lệ.");

        RuleFor(x => x.PenaltyType)
            .NotEmpty()
            .Must(t => ValidTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("PenaltyType phải là 'Warning', 'Suspension' hoặc 'Ban'.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải ghi lý do phạt.")
            .MaximumLength(1000).WithMessage("Lý do không được vượt quá 1000 ký tự.");

        RuleFor(x => x.EvidenceRef)
            .MaximumLength(500).WithMessage("EvidenceRef không được vượt quá 500 ký tự.");

        RuleFor(x => x.SuspensionDays)
            .NotNull().WithMessage("Phạt tạm khoá cần khai số ngày tạm khoá.")
            .GreaterThan(0).WithMessage("Số ngày tạm khoá phải lớn hơn 0.")
            .When(x => string.Equals(x.PenaltyType, "Suspension", StringComparison.OrdinalIgnoreCase));
    }
}
