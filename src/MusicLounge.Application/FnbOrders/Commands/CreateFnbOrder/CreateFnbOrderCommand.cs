using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

public sealed record OrderItemInput(Guid MenuItemId, int Quantity, string? Note);

public sealed record CreateFnbOrderCommand(
    Guid LoungeId,
    Guid? ShowId,
    Guid? ZoneId,
    string? TableNote,
    string PaymentMethod,
    string? Note,
    IReadOnlyList<OrderItemInput> Items
) : ICommand<Guid>;
