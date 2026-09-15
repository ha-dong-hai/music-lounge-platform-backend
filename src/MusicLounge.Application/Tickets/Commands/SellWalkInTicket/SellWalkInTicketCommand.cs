using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Commands.SellWalkInTicket;

/// <param name="ClientRequestId">
/// MLACP-410. Máy quầy sinh một mã cho mỗi lượt bán và gửi lại đúng mã đó khi bán lại vì mất phản hồi — máy chủ trả lại
/// lượt bán cũ thay vì thu tiền và tạo vé lần nữa. Không bắt buộc, để client cũ không gãy.
/// </param>
public sealed record SellWalkInTicketCommand(int PriceId, int Quantity, Guid? ClientRequestId = null)
    : ICommand<WalkInSaleResultDto>;
