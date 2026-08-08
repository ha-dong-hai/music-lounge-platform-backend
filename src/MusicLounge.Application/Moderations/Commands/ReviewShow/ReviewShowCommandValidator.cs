using FluentValidation;

namespace MusicLounge.Application.Moderations.Commands.ReviewShow;

public sealed class ReviewShowCommandValidator : AbstractValidator<ReviewShowCommand>
{
    private static readonly string[] ValidDecisions = ["Approved", "Rejected"];

    public ReviewShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0).WithMessage("ShowId không hợp lệ.");

        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => ValidDecisions.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là 'Approved' hoặc 'Rejected'.");

        RuleFor(x => x.ReviewNote)
            .MaximumLength(1000).WithMessage("Ghi chú duyệt không được vượt quá 1000 ký tự.");

        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .WithMessage("Phải ghi lý do khi từ chối.")
            .When(x => string.Equals(x.Decision, "Rejected", StringComparison.OrdinalIgnoreCase));
    }
}
