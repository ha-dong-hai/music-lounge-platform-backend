using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbOrders.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Queries.GetFnbOrders;

internal sealed class GetFnbOrdersQueryHandler
    : IRequestHandler<GetFnbOrdersQuery, PaginatedResult<FnbOrderDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetFnbOrdersQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<FnbOrderDto>> Handle(
        GetFnbOrdersQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        var isOwner = lounge.OwnerId == _currentUser.UserId;
        // MLACP-449: kiem ca vai tro, nhu 4 cho doc LoungeId con lai. Tu MLACP-449 chu phong tra cung co claim lounge_id
        // (phong tra cua chinh ho) — khong leo quyen vi chu da qua bang isOwner, nhung check nay phai noi dung y
        // "nhan vien duoc phan cong o phong tra nay", khong phai "ai co lounge_id trung".
        var isScopedStaff = _currentUser.Role == Roles.Staff && _currentUser.LoungeId == request.LoungeId;
        if (!isOwner && !isScopedStaff)
            throw new ForbiddenException("Bạn không có quyền xem order F&B của venue này.");

        FnbOrderStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<FnbOrderStatus>(request.Status, true, out var parsed))
            statusFilter = parsed;

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        // Newest first: GetPagedAsync orders DESCENDING (MLACP-411 — this comment used to read as insertion order and a
        // client took it literally). Kept that way on purpose: the same list is the Owner's order history, where page 1
        // must be recent orders; a bar board filters by status and sorts oldest-first on its side.
        // Keyed on Id (auto-increment, same sequence as CreatedAt) rather than CreatedAt itself — SQLite's EF Core
        // provider refuses ORDER BY on a DateTimeOffset column, a hard limitation independent of the query shape.
        var (pageItems, total) = await _uow.Repository<FnbOrder, int>().GetPagedAsync(
            o => o.LoungeId == request.LoungeId && (!statusFilter.HasValue || o.Status == statusFilter.Value),
            o => o.Id, page, pageSize, ct);

        // MLACP-357: dung chung voi GET /fnb-orders/my — hai man phai tra loi cung mot cau hoi
        // (dac biet IsPaid) theo cung mot cach. Xem FnbOrderDtoBuilder.
        var dtos = await FnbOrderDtoBuilder.BuildAsync(_uow, pageItems, ct);

        return new PaginatedResult<FnbOrderDto>(dtos, page, pageSize, total);
    }
}
