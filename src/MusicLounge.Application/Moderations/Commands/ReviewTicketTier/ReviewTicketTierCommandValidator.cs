using FluentValidation;

namespace MusicLounge.Application.Moderations.Commands.ReviewTicketTier;

public sealed class ReviewTicketTierCommandValidator : AbstractValidator<ReviewTicketTierCommand>
{
    private static readonly string[] ValidDecisions = ["Approved", "Rejected"];

    public ReviewTicketTierCommandValidator()
    {
        RuleFor(x => x.TierId).GreaterThan(0).WithMessage("TierId không hợp lệ.");

        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(d => ValidDecisions.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là 'Approved' hoặc 'Rejected'.");

        RuleFor(x => x.ReviewNote)
            .MaximumLength(1000).WithMessage("Ghi chú duyệt không được vượt quá 1000 ký tự.");

        // Cung quy uoc MLACP-79 cua duyet buoi dien: tu choi thi bat buoc ghi ly do de chu phong tra biet sua gi.
        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .WithMessage("Phải ghi lý do khi từ chối.")
            .When(x => string.Equals(x.Decision, "Rejected", StringComparison.OrdinalIgnoreCase));
    }
}
