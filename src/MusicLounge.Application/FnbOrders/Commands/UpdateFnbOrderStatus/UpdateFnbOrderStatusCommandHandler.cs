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
    private readonly IAsyncKeyedLock _lock;

    public UpdateFnbOrderStatusCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _lock = @lock;
    }

    public async Task<Unit> Handle(UpdateFnbOrderStatusCommand request, CancellationToken ct)
    {
        // MLACP-349: cung khoa voi IPN VNPay va voi luc khach tao link thanh toan — thieu no thi nhan
        // vien va IPN co the cung doc "chua tra" roi cung ghi nhan mot khoan thu.
        await using var _ = await _lock.AcquireAsync(FnbOrderPayments.LockKey(request.OrderId), ct);

        var orderRepo = _uow.Repository<FnbOrder, int>();
        var order = await orderRepo.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(FnbOrder), request.OrderId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(order.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), order.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, order.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền cập nhật order F&B của venue này.");

        var newStatus = Enum.Parse<FnbOrderStatus>(request.Status, ignoreCase: true);
        var now = DateTimeOffset.UtcNow;

        // MLACP-349: "da tra tien" hoi bang payments, khong bang buoc cua bep — xem FnbOrderPayments.
        var isPaid = FnbOrderPayments.IsPaid(
            order, await FnbOrderPayments.HasConfirmedPaymentAsync(_uow, order.Id, exceptPaymentId: null, ct));

        // Cancelled is a side-exit, not the next step in the Pending->Preparing->Served->Paid
        // sequence — allowed from any state before the order is actually paid, since there was
        // previously no way to void an order at all (customer changed mind / walked out).
        if (newStatus == FnbOrderStatus.Cancelled)
        {
            if (order.Status is FnbOrderStatus.Paid or FnbOrderStatus.Cancelled)
                throw new DomainException($"Không thể hủy order đang ở trạng thái '{order.Status}'.");

            // Don tra truoc qua VNPay nay khong con o Paid, nen chot o tren khong con bat duoc no. Huy
            // mot don khach da tra ma khong co duong hoan nao nghia la giu tien cua khach ma khong giao
            // mon.
            if (isPaid)
                throw new DomainException(
                    "Đơn này khách đã thanh toán online nên không thể huỷ ngang ở đây — huỷ lúc này sẽ " +
                    "giữ tiền của khách mà không giao món.");

            var live = await FnbOrderPayments.LiveOnlinePaymentAsync(_uow, order.Id, now, ct);
            if (live is not null)
                throw new ConflictException(
                    "Khách đang thanh toán online cho đơn này (link VNPay còn hiệu lực khoảng " +
                    $"{FnbOrderPayments.MinutesLeft(live, now)} phút). Huỷ lúc này thì khách vẫn có thể " +
                    "trả tiền cho một đơn đã huỷ — hãy chờ giao dịch kết thúc rồi huỷ.");

            order.Status = FnbOrderStatus.Cancelled;
            orderRepo.Update(order);

            var itemRepo = _uow.Repository<OrderItem, int>();
            var items = await itemRepo.FindAsync(i => i.FnbOrderId == order.Id, ct);
            foreach (var item in items)
            {
                item.Cancelled = true;
                itemRepo.Update(item);
            }

            await _uow.SaveChangesAsync(ct);
            await NotifyAudienceAsync(order, FnbOrderStatus.Cancelled, ct);
            // Luu lai SAU khi gui thong bao — xem ghi chu o nhanh duoi.
            await _uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        var currentIndex = Array.IndexOf(Sequence, order.Status);
        var newIndex = Array.IndexOf(Sequence, newStatus);

        if (currentIndex < 0 || newIndex != currentIndex + 1)
            throw new DomainException(
                $"Không thể chuyển từ '{order.Status}' sang '{newStatus}'. Chỉ được chuyển tuần tự Pending → Preparing → Served → Paid.");

        // Marking Paid used to be a bare status flip with no record anywhere else in the system —
        // a Staff member could flag an order Paid without collecting anything (inflates
        // GetOwnerAnalyticsQueryHandler's FnbRevenue), or collect real money and never flag it
        // Paid (skims cash with nothing to reconcile against). This Payment row doesn't feed the
        // ledger/settlement pipeline (F&B isn't a platform-commission product, same as walk-in
        // ticket sales) — it exists purely so "who marked what order Paid, for how much, when" is
        // an auditable record instead of a single mutable status field only Staff can see/edit.
        if (newStatus == FnbOrderStatus.Paid && !isPaid)
        {
            // MLACP-349: khach dang co mot link VNPay con tra duoc. Thu tien mat luc nay thi neu khach
            // (hoac ngan hang cua khach, cham hon mot nhip) hoan tat giao dich do, khach tra hai lan.
            // Toast/Square cung khoa hoa don khi mot giao dich dang chay; cho toi da bang thoi han
            // link re hon nhieu so voi mot khoan hoan tien.
            var live = await FnbOrderPayments.LiveOnlinePaymentAsync(_uow, order.Id, now, ct);
            if (live is not null)
                throw new ConflictException(
                    "Khách đang thanh toán online cho đơn này (link VNPay còn hiệu lực khoảng " +
                    $"{FnbOrderPayments.MinutesLeft(live, now)} phút). Thu tiền mặt lúc này có thể " +
                    "khiến khách trả hai lần — hãy để khách hoàn tất trên VNPay, hoặc chờ link hết hạn.");

            // Khoi nay chi chay khi THU TIEN MAT that — don da tra online thi buoc Paid chi la dong
            // don, khong co khoan thu nao de ghi.
            _uow.Repository<Payment, int>().Add(new Payment
            {
                OrderId = $"FNB-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                PayerId = order.AudienceUserId,
                GrossAmount = order.TotalAmount,
                // Cash, no platform commission on F&B (same convention as walk-in ticket sales) —
                // net equals gross, not left at the decimal default of 0.
                NetAmount = order.TotalAmount,
                // MLACP-349: truoc day la order.PaymentMethod — ma InitiateFnbOrderPayment tung dat
                // Gateway ngay khi khach bam "thanh toan", nen khach bo do roi tra tien mat thi ban ghi
                // tien mat mang Method = Gateway, khong co ma giao dich.
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Confirmed,
                ReferenceType = FnbOrderPayments.ReferenceType,
                ReferenceId = order.Id.ToString(),
                PaidAt = now,
                CreatedAt = now
            });
            order.PaymentMethod = PaymentMethod.Cash;
        }

        order.Status = newStatus;

        // MLACP-349: don da tra truoc thi phuc vu xong la het viec — dong luon, khong bat nhan vien bam
        // them mot buoc "Paid" cho mot don khong con gi de thu.
        if (newStatus == FnbOrderStatus.Served && isPaid)
            order.Status = FnbOrderStatus.Paid;

        orderRepo.Update(order);

        await _uow.SaveChangesAsync(ct);
        await NotifyAudienceAsync(order, newStatus, ct);

        // Luu SAU khi gui thong bao. NotificationService chi Add() dong thong bao vao change
        // tracker — hop dong ghi ro nguoi goi phai luu — va TransactionBehavior chi Begin/Commit,
        // CommitTransactionAsync cung khong goi SaveChanges. Luu truoc roi moi Notify nghia la
        // dong thong bao duoc them vao bo nho roi bien mat, khong bao loi gi ca.
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <param name="step">Buoc nhan vien vua lam — khong phai order.Status, vi don tra truoc duoc dong
    /// ngay khi phuc vu va khach van can biet "mon da duoc phuc vu".</param>
    private Task NotifyAudienceAsync(FnbOrder order, FnbOrderStatus step, CancellationToken ct)
    {
        // Staff-placed orders on behalf of a walk-in guest have no app account to notify.
        if (order.AudienceUserId is not { } audienceUserId) return Task.CompletedTask;

        var (title, body) = step switch
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
