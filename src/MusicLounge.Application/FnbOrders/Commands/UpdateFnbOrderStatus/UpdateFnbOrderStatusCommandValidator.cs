using FluentValidation;

namespace MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;

public sealed class UpdateFnbOrderStatusCommandValidator : AbstractValidator<UpdateFnbOrderStatusCommand>
{
    private static readonly string[] ValidStatuses = ["Pending", "Preparing", "Served", "Paid", "Cancelled"];

    public UpdateFnbOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status phải là 'Pending', 'Preparing', 'Served', 'Paid' hoặc 'Cancelled'.");

        // MLACP-631: huỷ phải có lý do (KiotViet/CUKCUK bắt chọn lý do khi huỷ món đã báo bếp; đơn khách gọi qua app
        // hiện thẳng trên màn quầy bar nên luôn coi là đã báo bar).
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .When(x => string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Cần ghi lý do huỷ đơn.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
