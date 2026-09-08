using FluentValidation;

namespace MusicLounge.Application.Admin.Commands.ReviewVenue;

public sealed class ReviewVenueCommandValidator : AbstractValidator<ReviewVenueCommand>
{
    private static readonly string[] ValidDecisions = ["Approved", "Rejected"];

    public ReviewVenueCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0).WithMessage("LoungeId không hợp lệ.");

        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => ValidDecisions.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là 'Approved' hoặc 'Rejected'.");

        RuleFor(x => x.ReviewNote)
            .MaximumLength(1000).WithMessage("Ghi chú duyệt không được vượt quá 1000 ký tự.");

        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .WithMessage("Phải ghi lý do khi từ chối hồ sơ phòng trà.")
            .When(x => string.Equals(x.Decision, "Rejected", StringComparison.OrdinalIgnoreCase));
    }
}
