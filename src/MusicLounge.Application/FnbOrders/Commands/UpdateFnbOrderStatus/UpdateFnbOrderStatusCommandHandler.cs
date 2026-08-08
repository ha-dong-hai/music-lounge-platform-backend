using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;

internal sealed class UpdateFnbOrderStatusCommandHandler : IRequestHandler<UpdateFnbOrderStatusCommand, Unit>
{
    private static readonly FnbOrderStatus[] Sequence =
        [FnbOrderStatus.Pending, FnbOrderStatus.Preparing, FnbOrderStatus.Served, FnbOrderStatus.Paid];

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateFnbOrderStatusCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateFnbOrderStatusCommand request, CancellationToken ct)
    {
        var orderRepo = _uow.Repository<FnbOrder, int>();
        var order = await orderRepo.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(FnbOrder), request.OrderId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(order.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), order.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, order.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền cập nhật order F&B của venue này.");

        var newStatus = Enum.Parse<FnbOrderStatus>(request.Status, ignoreCase: true);

        // Cancelled is a side-exit, not the next step in the Pending->Preparing->Served->Paid
        // sequence — allowed from any state before the order is actually paid, since there was
        // previously no way to void an order at all (customer changed mind / walked out).
        if (newStatus == FnbOrderStatus.Cancelled)
        {
            if (order.Status is FnbOrderStatus.Paid or FnbOrderStatus.Cancelled)
                throw new DomainException($"Không thể hủy order đang ở trạng thái '{order.Status}'.");

            order.Status = FnbOrderStatus.Cancelled;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            orderRepo.Update(order);

            var itemRepo = _uow.Repository<OrderItem, int>();
            var items = await itemRepo.FindAsync(i => i.FnbOrderId == order.Id, ct);
            foreach (var item in items)
            {
                item.Cancelled = true;
                itemRepo.Update(item);
            }

            await _uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        var currentIndex = Array.IndexOf(Sequence, order.Status);
        var newIndex = Array.IndexOf(Sequence, newStatus);

        if (currentIndex < 0 || newIndex != currentIndex + 1)
            throw new DomainException(
                $"Không thể chuyển từ '{order.Status}' sang '{newStatus}'. Chỉ được chuyển tuần tự Pending → Preparing → Served → Paid.");

        order.Status = newStatus;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        orderRepo.Update(order);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
