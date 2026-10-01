using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbOrders.DTOs;

namespace MusicLounge.Application.FnbOrders.Queries.GetMyFnbOrders;

/// <summary>MLACP-357 — đơn F&amp;B của chính người đang đăng nhập, mới nhất trước. MLACP-500: <c>LoungeId</c> tuỳ chọn,
/// chỉ lấy đơn ở đúng phòng trà đó.</summary>
public sealed record GetMyFnbOrdersQuery(int Page = 1, int PageSize = 20, int? LoungeId = null)
    : IQuery<PaginatedResult<FnbOrderDto>>;
