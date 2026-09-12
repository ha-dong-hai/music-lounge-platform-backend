using FluentValidation;

namespace MusicLounge.Application.Refunds.Commands.ProvideRefundPayoutAccount;

internal sealed class ProvideRefundPayoutAccountCommandValidator : AbstractValidator<ProvideRefundPayoutAccountCommand>
{
    public ProvideRefundPayoutAccountCommandValidator()
    {
        RuleFor(x => x.RefundRequestId).GreaterThan(0);

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Cần tên ngân hàng nhận hoàn.")
            .MaximumLength(100);

        // So tai khoan ngan hang Viet Nam chi gom chu so; do dai tuy ngan hang.
        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Cần số tài khoản nhận hoàn.")
            .Matches("^[0-9]{6,20}$").WithMessage("Số tài khoản chỉ gồm chữ số, từ 6 đến 20 số.");

        RuleFor(x => x.AccountHolder)
            .NotEmpty().WithMessage("Cần tên chủ tài khoản.")
            .MaximumLength(100);

        RuleFor(x => x.Consent)
            .Equal(true)
            .WithMessage("Cần xác nhận đồng ý nhận hoàn bằng chuyển khoản vào tài khoản này.");
    }
}
