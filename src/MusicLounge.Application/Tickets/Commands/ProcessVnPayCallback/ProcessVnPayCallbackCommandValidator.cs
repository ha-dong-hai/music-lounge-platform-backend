using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.ProcessVnPayCallback;

public sealed class ProcessVnPayCallbackCommandValidator
    : AbstractValidator<ProcessVnPayCallbackCommand>
{
    public ProcessVnPayCallbackCommandValidator()
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
