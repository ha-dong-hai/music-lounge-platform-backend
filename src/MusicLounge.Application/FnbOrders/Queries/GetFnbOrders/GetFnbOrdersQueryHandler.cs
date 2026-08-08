using MediatR;
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
        var isScopedStaff = _currentUser.LoungeId == request.LoungeId;
        if (!isOwner && !isScopedStaff)
            throw new ForbiddenException("Bạn không có quyền xem order F&B của venue này.");

        FnbOrderStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<FnbOrderStatus>(request.Status, true, out var parsed))
            statusFilter = parsed;

        var orders = await _uow.Repository<FnbOrder, int>().FindAsync(
            o => o.LoungeId == request.LoungeId && (!statusFilter.HasValue || o.Status == statusFilter.Value), ct);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var ordered = orders.OrderByDescending(o => o.CreatedAt).ToList();
        var total = ordered.Count;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var orderIds = pageItems.Select(o => o.Id).ToList();
        var items = await _uow.Repository<OrderItem, int>().FindAsync(i => orderIds.Contains(i.FnbOrderId), ct);
        var menuItemIds = items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _uow.Repository<FnbMenuItem, int>().FindAsync(m => menuItemIds.Contains(m.Id), ct);
        var menuItemsById = menuItems.ToDictionary(m => m.Id);
        var itemsByOrder = items.ToLookup(i => i.FnbOrderId);

        var dtos = pageItems.Select(o => new FnbOrderDto(
            o.Id, o.LoungeId, o.ShowId, o.AudienceUserId, o.StaffId, o.TableNote,
            o.Status.ToString(), o.PaymentMethod.ToString(), o.TotalAmount, o.Note, o.CreatedAt,
            itemsByOrder[o.Id].Select(i => new OrderItemDto(
                i.Id, i.MenuItemId,
                menuItemsById.TryGetValue(i.MenuItemId, out var mi) ? mi.Name : "(deleted)",
                i.Quantity, i.UnitPrice, i.Cancelled, i.Note))
            .ToList())
        ).ToList();

        return new PaginatedResult<FnbOrderDto>(dtos, page, pageSize, total);
    }
}
