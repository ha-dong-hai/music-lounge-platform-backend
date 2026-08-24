using FluentValidation;

namespace MusicLounge.Application.Moderations.Commands.ReviewShow;

// MLACP-63 (co ban): chua bat buoc ReviewNote khi Rejected — se sua thanh bat buoc o MLACP-79
// ("kem ly do bat buoc"), tranh vua viet 1 rule vua bo di ngay commit sau.
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
    }
}
