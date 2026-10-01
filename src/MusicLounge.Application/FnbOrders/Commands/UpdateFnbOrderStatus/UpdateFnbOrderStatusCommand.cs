using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;

public sealed record UpdateFnbOrderStatusCommand(Guid OrderId, string Status) : ICommand;
