using MusicLounge.Application.Common;
using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.FnbOrders.Commands.CancelMyFnbOrder;

/// <summary>
/// MLACP-631 — khách tự huỷ đơn gọi món của chính mình, CHỈ khi quầy chưa nhận (còn "Chờ quầy nhận").
///
/// Thực tế: ShopeeFood cho khách tự huỷ khi đơn còn "Đang chờ xác nhận", quán đã nhận thì không còn nút huỷ tự động. Ở
/// phòng trà cũng vậy: quầy bar đã bắt đầu pha là nguyên liệu đã dùng — muốn bỏ thì khách nói với nhân viên, và chỉ chủ
/// phòng trà quyết huỷ (UpdateFnbOrderStatusCommandHandler). Trước task này khách không có cách nào tự huỷ, kể cả đơn gửi
/// nhầm một phút trước.
/// </summary>
public sealed record CancelMyFnbOrderCommand(Guid OrderId) : ICommand;

internal sealed class CancelMyFnbOrderCommandHandler(
    IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock, INotificationService notifications)
    : IRequestHandler<CancelMyFnbOrderCommand, Unit>
{
    public const string Reason = "Khách tự huỷ khi quầy chưa nhận đơn";

    public async Task<Unit> Handle(CancelMyFnbOrderCommand request, CancellationToken ct)
    {
        // Cùng khoá với nhân viên đổi trạng thái / khách tạo link VNPay / IPN — không thì quầy vừa bấm "Bắt đầu làm"
        // đúng lúc khách bấm huỷ, và một đơn đang được pha lại thành đã huỷ.
        await using var _ = await @lock.AcquireAsync(FnbOrderPayments.LockKey(request.OrderId), ct);

        var order = await uow.Repository<FnbOrder, Guid>().GetByIdAsync(request.OrderId, ct);
        // Đơn của người khác trả 404 như đơn không tồn tại — không xác nhận cho người lạ rằng mã đơn đó có thật.
        if (order is null || order.AudienceUserId != currentUser.UserId)
            throw new NotFoundException(nameof(FnbOrder), request.OrderId);

        if (order.Status != FnbOrderStatus.Pending)
            throw new DomainException(order.Status == FnbOrderStatus.Cancelled
                ? "Đơn này đã được huỷ."
                : "Quầy đã nhận đơn và đang làm — bạn không tự huỷ được nữa. Hãy nói với nhân viên phục vụ.");

        var now = DateTimeOffset.UtcNow;
        var live = await FnbOrderPayments.LiveOnlinePaymentAsync(uow, order.Id, now, ct);
        if (live is not null)
            throw new ConflictException(
                "Bạn đang có một lượt thanh toán VNPay cho đơn này chưa kết thúc (còn khoảng " +
                $"{FnbOrderPayments.MinutesLeft(live, now)} phút). Hãy hoàn tất hoặc đóng trang thanh toán rồi huỷ đơn.");

        var isPaid = FnbOrderPayments.IsPaid(
            order, await FnbOrderPayments.HasConfirmedPaymentAsync(uow, order.Id, exceptPaymentId: null, ct));

        var refundAmount = await FnbOrderCancellation.CancelOneAsync(
            uow, order, isPaid, currentUser.UserId, Reason,
            $"Khách tự huỷ đơn F&B #{order.Id} khi quầy chưa nhận — hoàn 100%", now, ct);
        await uow.SaveChangesAsync(ct);

        // Khách tự bấm nên không cần báo "đơn đã huỷ"; chỉ báo khi có tiền hoàn — khách cần biết tiền về đâu, bao giờ.
        if (refundAmount is { } amount)
        {
            await notifications.NotifyAsync(
                currentUser.UserId, NotificationType.FnbOrderUpdate,
                new SongNgu("Đã huỷ đơn — bạn sẽ được hoàn tiền", "Order cancelled — you will be refunded"),
                new SongNgu(
                    $"Đơn #{order.Id} đã huỷ. Chúng tôi đã tạo yêu cầu hoàn 100% ({VietnamMoney.Format(amount)}) về phương thức bạn đã " +
                    "thanh toán và sẽ báo khi yêu cầu được xử lý.",
                    $"Order #{order.Id} has been cancelled. We have created a 100% refund request ({amount:N0} VND) to " +
                    "your original payment method and will notify you when it is processed."),
                referenceType: "fnb_order", referenceId: order.Id.ToString(), ct: ct);
            await uow.SaveChangesAsync(ct);
        }
        return Unit.Value;
    }
}
