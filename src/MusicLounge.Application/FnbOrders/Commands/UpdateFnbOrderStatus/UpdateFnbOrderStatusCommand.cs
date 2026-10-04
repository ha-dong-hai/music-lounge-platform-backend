using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;

/// <param name="Reason">MLACP-631: bắt buộc khi Status = Cancelled — lý do huỷ được lưu vào đơn và hiện cho chủ phòng trà.</param>
public sealed record UpdateFnbOrderStatusCommand(Guid OrderId, string Status, string? Reason = null) : ICommand;
