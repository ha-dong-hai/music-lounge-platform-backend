using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

internal sealed class CreateFnbOrderCommandHandler : IRequestHandler<CreateFnbOrderCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateFnbOrderCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateFnbOrderCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        // MLACP-380: cung quy uoc MLACP-354 da dat cho ve/donation — F&B bi bo sot. Cau cho nguoi mua khac cau cho
        // nhan vien/chu phong tra (ExplainRestriction), giong SellWalkInTicketCommandHandler.
        if (!VenueLifecycle.CanOperate(lounge.Status))
        {
            var isStaffSide = _currentUser.Role is Roles.Staff or Roles.Owner or Roles.Admin;
            throw new DomainException(isStaffSide
                ? $"{VenueLifecycle.ExplainRestriction(lounge.Status)} Không thể tạo order F&B lúc này."
                : VenueLifecycle.TradingPausedForBuyers);
        }

        // ZoneId/ShowId were accepted as-is with no check they actually belong to this LoungeId
        // (unlike MenuItemId below, which is validated) — order could silently point at another
        // venue's zone/show, showing up on the wrong venue's floor display.
        if (request.ZoneId.HasValue)
        {
            var zone = await _uow.Repository<SeatingZone, int>().GetByIdAsync(request.ZoneId.Value, ct)
                ?? throw new NotFoundException(nameof(SeatingZone), request.ZoneId.Value);
            if (zone.LoungeId != request.LoungeId)
                throw new DomainException("Khu vực này không thuộc venue này.");
        }

        if (request.ShowId.HasValue)
        {
            var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId.Value, ct)
                ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId.Value);
            if (show.LoungeId != request.LoungeId)
                throw new DomainException("Show này không thuộc venue này.");

            // MLACP-390: don gan voi mot buoi dien la phuc vu khach tai cho cua buoi dien do. Buoi dien chi dien online
            // hoac da bi huy thi khong co ai tai cho — va don se khong con duoc huy/hoan theo buoi dien nua.
            if (show.Format == LoungeShowFormat.Online || show.Status == LoungeShowStatus.Cancelled)
                throw new DomainException(
                    "Buổi diễn này không có khán giả tại chỗ (chỉ diễn online hoặc đã bị huỷ) — không nhận order F&B " +
                    "gắn với buổi diễn này.");
        }

        int? audienceUserId = null;
        int? staffId = null;

        if (_currentUser.Role is Roles.Staff or Roles.Owner or Roles.Admin)
        {
            if (!VenueOperatorAccess.CanOperate(_currentUser, request.LoungeId, lounge.OwnerId))
                throw new ForbiddenException("Bạn không có quyền tạo order F&B cho venue này.");
            staffId = _currentUser.UserId;
        }
        else
        {
            audienceUserId = _currentUser.UserId;
        }

        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _uow.Repository<FnbMenuItem, int>()
            .FindAsync(m => menuItemIds.Contains(m.Id), ct);
        var menuItemsById = menuItems.ToDictionary(m => m.Id);

        var menuIds = menuItems.Select(m => m.MenuId).Distinct().ToList();
        var menus = await _uow.Repository<FnbMenu, int>()
            .FindAsync(m => menuIds.Contains(m.Id), ct);
        var menusById = menus.ToDictionary(m => m.Id);

        foreach (var id in menuItemIds)
        {
            if (!menuItemsById.TryGetValue(id, out var item))
                throw new NotFoundException(nameof(FnbMenuItem), id);
            if (!menusById.TryGetValue(item.MenuId, out var menu) || menu.LoungeId != request.LoungeId)
                throw new DomainException($"Món #{id} không thuộc venue này.");
            if (!item.IsAvailable)
                throw new DomainException($"Món '{item.Name}' hiện không có sẵn.");
        }

        var paymentMethod = Enum.Parse<PaymentMethod>(request.PaymentMethod, ignoreCase: true);

        var order = new FnbOrder
        {
            LoungeId = request.LoungeId,
            ShowId = request.ShowId,
            AudienceUserId = audienceUserId,
            StaffId = staffId,
            ZoneId = request.ZoneId,
            TableNote = request.TableNote,
            Status = FnbOrderStatus.Pending,
            PaymentMethod = paymentMethod,
            Note = request.Note
        };

        _uow.Repository<FnbOrder, int>().Add(order);
        await _uow.SaveChangesAsync(ct);

        decimal total = 0m;
        foreach (var input in request.Items)
        {
            var menuItem = menuItemsById[input.MenuItemId];
            total += menuItem.Price * input.Quantity;

            _uow.Repository<OrderItem, int>().Add(new OrderItem
            {
                FnbOrderId = order.Id,
                MenuItemId = input.MenuItemId,
                Quantity = input.Quantity,
                UnitPrice = menuItem.Price,
                Note = input.Note
            });
        }

        order.TotalAmount = total;
        _uow.Repository<FnbOrder, int>().Update(order);
        await _uow.SaveChangesAsync(ct);

        return order.Id;
    }
}
