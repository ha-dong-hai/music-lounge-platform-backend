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
    private readonly INotificationService _notifications;

    public UpdateFnbOrderStatusCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
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
            await NotifyAudienceAsync(order, ct);
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

        // Marking Paid used to be a bare status flip with no record anywhere else in the system —
        // a Staff member could flag an order Paid without collecting anything (inflates
        // GetOwnerAnalyticsQueryHandler's FnbRevenue), or collect real money and never flag it
        // Paid (skims cash with nothing to reconcile against). This Payment row doesn't feed the
        // ledger/settlement pipeline (F&B isn't a platform-commission product, same as walk-in
        // ticket sales) — it exists purely so "who marked what order Paid, for how much, when" is
        // an auditable record instead of a single mutable status field only Staff can see/edit.
        if (newStatus == FnbOrderStatus.Paid)
        {
            _uow.Repository<Payment, int>().Add(new Payment
            {
                OrderId = $"FNB-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                PayerId = order.AudienceUserId,
                GrossAmount = order.TotalAmount,
                // Cash, no platform commission on F&B (same convention as walk-in ticket sales) —
                // net equals gross, not left at the decimal default of 0.
                NetAmount = order.TotalAmount,
                Method = order.PaymentMethod,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "FnbOrder",
                ReferenceId = order.Id.ToString(),
                PaidAt = order.UpdatedAt,
                CreatedAt = order.UpdatedAt
            });
        }

        await _uow.SaveChangesAsync(ct);
        await NotifyAudienceAsync(order, ct);
        return Unit.Value;
    }

    private Task NotifyAudienceAsync(FnbOrder order, CancellationToken ct)
    {
        // Staff-placed orders on behalf of a walk-in guest have no app account to notify.
        if (order.AudienceUserId is not { } audienceUserId) return Task.CompletedTask;

        var (title, body) = order.Status switch
        {
            FnbOrderStatus.Preparing => ("Đơn F&B đang được chuẩn bị", $"Đơn #{order.Id} của bạn đang được chuẩn bị."),
            FnbOrderStatus.Served => ("Đơn F&B đã phục vụ", $"Đơn #{order.Id} của bạn đã được phục vụ."),
            FnbOrderStatus.Cancelled => ("Đơn F&B đã bị hủy", $"Đơn #{order.Id} của bạn đã bị hủy."),
            _ => (null, null)
        };
        if (title is null) return Task.CompletedTask;

        return _notifications.NotifyAsync(
            audienceUserId, NotificationType.FnbOrderUpdate, title, body!,
            referenceType: "fnbOrder", referenceId: order.Id.ToString(), ct: ct);
    }
}
