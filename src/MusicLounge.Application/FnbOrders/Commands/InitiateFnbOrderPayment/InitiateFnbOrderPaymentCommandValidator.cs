using FluentValidation;

namespace MusicLounge.Application.FnbOrders.Commands.InitiateFnbOrderPayment;

public sealed class InitiateFnbOrderPaymentCommandValidator : AbstractValidator<InitiateFnbOrderPaymentCommand>
{
    public InitiateFnbOrderPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId không hợp lệ.");
        RuleFor(x => x.ClientIpAddress).NotEmpty();
    }
}
