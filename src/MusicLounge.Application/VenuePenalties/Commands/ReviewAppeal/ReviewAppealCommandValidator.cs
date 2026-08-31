using FluentValidation;

namespace MusicLounge.Application.VenuePenalties.Commands.ReviewAppeal;

public sealed class ReviewAppealCommandValidator : AbstractValidator<ReviewAppealCommand>
{
    private static readonly string[] ValidDecisions = ["Overturned", "Upheld"];

    public ReviewAppealCommandValidator()
    {
        RuleFor(x => x.PenaltyId).GreaterThan(0).WithMessage("PenaltyId không hợp lệ.");

        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => ValidDecisions.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là 'Overturned' hoặc 'Upheld'.");

        RuleFor(x => x.ReviewNote).MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
