using FluentValidation;

namespace MusicLounge.Application.Donations.Commands.ConfirmDonationPaid;

public sealed class ConfirmDonationPaidCommandValidator : AbstractValidator<ConfirmDonationPaidCommand>
{
    public ConfirmDonationPaidCommandValidator()
    {
        RuleFor(x => x.DonationId).GreaterThan(0).WithMessage("DonationId không hợp lệ.");
        RuleFor(x => x.PaymentRef).NotEmpty().WithMessage("Mã tham chiếu thanh toán không được rỗng.")
            .MaximumLength(255).WithMessage("Mã tham chiếu thanh toán không được vượt quá 255 ký tự.");
        RuleFor(x => x.PaymentEvidenceUrl)
            .MaximumLength(500).WithMessage("URL bằng chứng thanh toán không được vượt quá 500 ký tự.")
            .When(x => x.PaymentEvidenceUrl is not null);
    }
}
