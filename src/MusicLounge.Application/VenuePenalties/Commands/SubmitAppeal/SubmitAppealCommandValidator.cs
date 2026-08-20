using FluentValidation;

namespace MusicLounge.Application.VenuePenalties.Commands.SubmitAppeal;

public sealed class SubmitAppealCommandValidator : AbstractValidator<SubmitAppealCommand>
{
    public SubmitAppealCommandValidator()
    {
        RuleFor(x => x.PenaltyId).GreaterThan(0).WithMessage("PenaltyId không hợp lệ.");

        RuleFor(x => x.AppealReason)
            .NotEmpty().WithMessage("Phải ghi lý do kháng cáo.")
            .MaximumLength(1000).WithMessage("Lý do kháng cáo không được vượt quá 1000 ký tự.");
    }
}
