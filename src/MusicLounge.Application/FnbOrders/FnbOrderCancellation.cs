using System.Linq.Expressions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.FnbOrders;

/// <summary>
/// Huỷ hàng loạt đơn F&amp;B chưa đóng khi thứ đơn phục vụ không còn: buổi diễn bị huỷ (MLACP-380), buổi diễn chuyển
/// sang online (MLACP-390). Tách nguyên văn từ <c>ShowCancellation</c> để các đường đó đi cùng một luật.
///
/// <para>Đơn đã đóng (<see cref="FnbOrderStatus.Paid"/>) là giao dịch đã xong — dù đóng bằng tiền mặt hay online —
/// không đụng tới, giữ đúng ranh giới <c>UpdateFnbOrderStatusCommandHandler</c> đã đặt cho việc huỷ một đơn F&amp;B
/// (MLACP-349/351: chỉ huỷ được đơn CHƯA đóng). Đơn chưa đóng mà đã có một giao dịch Gateway Confirmed (khách trả
/// trước qua VNPay, bếp chưa kịp phục vụ xong) thì hoàn 100% — cùng logic đã có ở đó. Đơn chưa đóng và chưa có giao
/// dịch nào thì huỷ thẳng, không có gì để hoàn. Nơi gọi tự kiểm quyền, giữ khoá của mình và tự lưu.</para>
/// </summary>
internal static class FnbOrderCancellation
{
    /// <param name="lock">Mỗi đơn bị đụng tới phải khoá đúng key <c>fnb-order:{id}</c> — cùng khoá với lúc khách trả
    /// tiền/nhân viên đổi trạng thái/IPN VNPay, tránh một đơn vừa bị huỷ ở đây vừa được xử lý ở một trong ba đường đó
    /// cùng lúc.</param>
    /// <param name="scope">Đơn nào thuộc diện, ví dụ mọi đơn gắn với một buổi diễn.</param>
    /// <param name="servedToo">Có huỷ cả đơn đã phục vụ nhưng chưa đóng không. Huỷ buổi diễn giữ đúng như MLACP-380 đã
    /// ship: có. Chuyển online: không — món đã mang ra là hàng đã giao, phòng trà vẫn thu tiền được.</param>
    /// <param name="why">Cụm lý do viết thường, ghép sau "vì" trong thông báo cho khách, ví dụ "buổi diễn bị huỷ".</param>
    /// <returns>Số đơn đã huỷ.</returns>
    public static async Task<int> CancelOpenOrdersAsync(
        IUnitOfWork uow, INotificationService notifications, IAsyncKeyedLock @lock,
        Expression<Func<FnbOrder, bool>> scope, bool servedToo, string why, CancellationToken ct)
    {
        var orderRepo = uow.Repository<FnbOrder, int>();
        var orders = (await orderRepo.FindAsync(scope, ct)).Where(o => IsOpen(o.Status, servedToo)).ToList();
        if (orders.Count == 0) return 0;

        var itemRepo = uow.Repository<OrderItem, int>();
        var paymentRepo = uow.Repository<Payment, int>();
        var refundRepo = uow.Repository<RefundRequest, int>();
        var reason = char.ToUpperInvariant(why[0]) + why[1..];
        var affected = 0;

        foreach (var order in orders)
        {
            // Cung khoa voi InitiateFnbOrderPayment / UpdateFnbOrderStatus / ProcessFnbOrderPayment (IPN) —
            // khong thi mot don co the vua bi huy o day vua duoc xu ly o mot trong ba noi do cung luc.
            await using var _ = await @lock.AcquireAsync(FnbOrderPayments.LockKey(order.Id), ct);

            // Doc lai sau khi co khoa: don co the da doi trang thai (vi du da duoc danh dau Paid) giua luc
            // truy van o tren va luc lay duoc khoa nay.
            var current = await orderRepo.GetByIdAsync(order.Id, ct);
            if (current is null || !IsOpen(current.Status, servedToo)) continue;

            var referenceId = current.Id.ToString();
            var gatewayPayment = (await paymentRepo.FindAsync(
                    p => p.ReferenceType == FnbOrderPayments.ReferenceType
                         && p.ReferenceId == referenceId
                         && p.Status == PaymentStatus.Confirmed
                         && p.Method == PaymentMethod.Gateway, ct))
                .FirstOrDefault();

            current.Status = FnbOrderStatus.Cancelled;
            orderRepo.Update(current);

            var items = await itemRepo.FindAsync(i => i.FnbOrderId == current.Id, ct);
            foreach (var item in items)
            {
                item.Cancelled = true;
                itemRepo.Update(item);
            }

            decimal? refundAmount = null;
            if (gatewayPayment is not null)
            {
                refundAmount = gatewayPayment.GrossAmount;
                refundRepo.Add(new RefundRequest
                {
                    PaymentId = gatewayPayment.Id,
                    RequestedBy = current.AudienceUserId ?? gatewayPayment.PayerId,
                    Reason = $"{reason} — đơn F&B #{current.Id} chưa phục vụ xong, hoàn 100%",
                    AmountRequested = gatewayPayment.GrossAmount,
                    RefundPercentage = 100m,
                    Status = RefundRequestStatus.Pending
                });
            }

            affected++;

            // Don nhan vien dat ho khach vang lai (khong tai khoan) khong bao duoc — giong quy uoc
            // NotifyAudienceAsync cua UpdateFnbOrderStatusCommandHandler.
            if (current.AudienceUserId is { } audienceUserId)
            {
                var (title, body) = refundAmount is { } amount
                    ? ("Đơn F&B đã bị hủy — bạn sẽ được hoàn tiền",
                       $"Đơn #{current.Id} của bạn đã bị hủy vì {why}. Chúng tôi đã tự động " +
                       $"tạo yêu cầu hoàn 100% ({amount:N0}đ) về phương thức bạn đã thanh toán — bạn không cần " +
                       "làm gì thêm và sẽ được báo khi yêu cầu được xử lý.")
                    : ("Đơn F&B đã bị hủy",
                       $"Đơn #{current.Id} của bạn đã bị hủy vì {why}.");

                await notifications.NotifyAsync(
                    audienceUserId, NotificationType.FnbOrderUpdate, title, body,
                    referenceType: "fnbOrder", referenceId: current.Id.ToString(), ct: ct);
            }
        }

        return affected;
    }

    /// <summary>Chưa đóng: chưa trả xong và chưa huỷ. Đơn đã phục vụ chỉ tính khi nơi gọi yêu cầu.</summary>
    private static bool IsOpen(FnbOrderStatus status, bool servedToo)
        => status is FnbOrderStatus.Pending or FnbOrderStatus.Preparing
           || (servedToo && status == FnbOrderStatus.Served);
}
