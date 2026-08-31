using FluentValidation;

namespace MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;

public sealed class ProcessFnbOrderPaymentCommandValidator
    : AbstractValidator<ProcessFnbOrderPaymentCommand>
{
    public ProcessFnbOrderPaymentCommandValidator()
    {
        RuleFor(x => x.QueryParams)
            .NotNull()
            .WithMessage("Query params không được null.");

        RuleFor(x => x.QueryParams)
            .Must(p => p is not null && p.ContainsKey("vnp_TxnRef"))
            .WithMessage("Thiếu tham số vnp_TxnRef từ VNPay.")
            .When(x => x.QueryParams is not null);
    }
}
