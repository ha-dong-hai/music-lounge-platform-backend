using FluentValidation;

namespace MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

public sealed class CreateFnbOrderCommandValidator : AbstractValidator<CreateFnbOrderCommand>
{
    private static readonly string[] ValidMethods = ["Gateway", "Cash"];

    public CreateFnbOrderCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);

        RuleFor(x => x.PaymentMethod)
            .Must(m => ValidMethods.Contains(m, StringComparer.OrdinalIgnoreCase))
            .WithMessage("PaymentMethod phải là 'Gateway' hoặc 'Cash'.");

        RuleFor(x => x.Items).NotEmpty().WithMessage("Đơn hàng phải có ít nhất 1 món.");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.MenuItemId).GreaterThan(0);
            i.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}
