using FluentValidation;

namespace MusicLounge.Application.Subscriptions.Commands.ProcessSubscriptionPayment;

public sealed class ProcessSubscriptionPaymentCommandValidator
    : AbstractValidator<ProcessSubscriptionPaymentCommand>
{
    public ProcessSubscriptionPaymentCommandValidator()
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
