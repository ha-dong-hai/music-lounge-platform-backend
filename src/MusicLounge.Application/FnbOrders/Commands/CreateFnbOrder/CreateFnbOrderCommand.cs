using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

public sealed record OrderItemInput(int MenuItemId, int Quantity, string? Note);

public sealed record CreateFnbOrderCommand(
    int LoungeId,
    int? ShowId,
    int? ZoneId,
    string? TableNote,
    string PaymentMethod,
    string? Note,
    IReadOnlyList<OrderItemInput> Items
) : ICommand<int>;
