using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbOrders.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.FnbOrders.Queries.GetMyFnbOrders;

/// <summary>
/// MLACP-357. Trước đây <c>GET /fnb-orders</c> chỉ dành cho chủ phòng trà và nhân viên đúng phòng trà —
/// khán giả không có cách nào xem đơn của chính mình, kể cả đơn đã trả tiền online, và chỉ biết tình
/// trạng đơn qua thông báo.
///
/// <para>Chỉ đơn có <c>AudienceUserId</c> là người gọi: đơn nhân viên tạo hộ khách vãng lai không gắn
/// với tài khoản nào, nên không nằm trong danh sách của ai.</para>
/// </summary>
internal sealed class GetMyFnbOrdersQueryHandler
    : IRequestHandler<GetMyFnbOrdersQuery, PaginatedResult<FnbOrderDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMyFnbOrdersQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<FnbOrderDto>> Handle(GetMyFnbOrdersQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Sắp theo Id giảm dần (mới nhất trước) — cùng lý do GetFnbOrdersQueryHandler: provider SQLite
        // không dịch được ORDER BY trên cột DateTimeOffset, còn Id tự tăng đã đúng thứ tự tạo.
        var (pageItems, total) = await _uow.Repository<FnbOrder, int>().GetPagedAsync(
            o => o.AudienceUserId == userId, o => o.Id, page, pageSize, ct);

        var dtos = await FnbOrderDtoBuilder.BuildAsync(_uow, pageItems, ct);
        return new PaginatedResult<FnbOrderDto>(dtos, page, pageSize, total);
    }
}
