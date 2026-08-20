using FluentValidation;

namespace MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;

public sealed class UpdateFnbOrderStatusCommandValidator : AbstractValidator<UpdateFnbOrderStatusCommand>
{
    private static readonly string[] ValidStatuses = ["Pending", "Preparing", "Served", "Paid", "Cancelled"];

    public UpdateFnbOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status phải là 'Pending', 'Preparing', 'Served', 'Paid' hoặc 'Cancelled'.");
    }
}
