using FluentValidation;

namespace MusicLounge.Application.Moderations.Commands.ReviewLivestream;

internal sealed class ReviewLivestreamCommandValidator : AbstractValidator<ReviewLivestreamCommand>
{
    private static readonly string[] ValidDecisions = ["Approved", "Rejected"];

    public ReviewLivestreamCommandValidator()
    {
        RuleFor(x => x.LivestreamId).GreaterThan(0).WithMessage("LivestreamId không hợp lệ.");

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
