using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetShowOrders;

/// <summary>
/// Danh sách người đã mua vé một buổi diễn, cho chủ phòng trà đối soát và đón khách. Khác
/// GetShowTicketStats vốn chỉ trả con số tổng.
/// </summary>
public sealed record GetShowOrdersQuery(int ShowId, int Page = 1, int PageSize = 50)
    : IQuery<PaginatedResult<ShowOrderDto>>;
