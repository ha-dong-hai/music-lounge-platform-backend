using FluentValidation;

namespace MusicLounge.Application.FnbOrders.Commands.InitiateFnbOrderPayment;

public sealed class InitiateFnbOrderPaymentCommandValidator : AbstractValidator<InitiateFnbOrderPaymentCommand>
{
    public InitiateFnbOrderPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("OrderId không hợp lệ.");
        RuleFor(x => x.ClientIpAddress).NotEmpty();
    }
}
