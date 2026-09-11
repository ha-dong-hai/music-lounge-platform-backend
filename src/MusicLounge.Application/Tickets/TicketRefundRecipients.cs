using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets;

/// <summary>
/// MLACP-370 — tiền hoàn của một vé luôn đi về <b>người đã trả tiền</b>, không phải người đang giữ vé.
///
/// <para>VNPay chỉ hoàn về đúng giao dịch gốc, nên với vé đã chuyển nhượng, tiền về phương thức thanh toán
/// của người mua ban đầu. Trước đây mọi đường hoàn do nền tảng ép (huỷ show, đổi hình thức, gỡ nội dung,
/// khiếu nại, livestream không phát / bị cắt ngang) ghi yêu cầu hoàn dưới tên người <b>đang giữ</b> vé và chỉ
/// báo cho họ "đã tạo yêu cầu hoàn 100% tiền vé" — người nhận chuyển nhượng tưởng tiền về mình, còn người
/// mua ban đầu không được báo gì dù tiền về tài khoản của họ.</para>
///
/// <para>Căn cứ: Ticketmaster — "we'd refund the person who purchased the tickets directly from us"; người
/// nhận chuyển nhượng phải "transfer them back to the original purchaser".</para>
/// </summary>
public static class TicketRefundRecipients
{
    /// <summary>Câu nối vào thông báo cho người đang giữ một vé đã chuyển nhượng.</summary>
    public const string TransferredHolderNote =
        " Vé này được chuyển nhượng cho bạn, nên tiền được hoàn về người đã mua vé ban đầu.";

    /// <summary>Người đã trả tiền của từng thanh toán mà các vé này thuộc về.</summary>
    public static async Task<IReadOnlyDictionary<int, int?>> PayersAsync(
        IUnitOfWork uow, IEnumerable<Ticket> tickets, CancellationToken ct)
    {
        var paymentIds = tickets.Where(t => t.PaymentId is not null).Select(t => t.PaymentId!.Value).Distinct().ToList();
        if (paymentIds.Count == 0) return new Dictionary<int, int?>();
        return (await uow.Repository<Payment, int>().FindAsync(p => paymentIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id, p => p.PayerId);
    }

    /// <summary>Người nhận lại tiền: người đã trả; không rõ người trả thì người đang giữ vé.</summary>
    public static int? RefundedTo(Ticket ticket, IReadOnlyDictionary<int, int?> payers)
        => ticket.PaymentId is int paymentId && payers.GetValueOrDefault(paymentId) is int payer
            ? payer
            : ticket.BuyerId;

    /// <summary>Vé đã qua tay người khác: người đang giữ không phải người đã trả tiền.</summary>
    public static bool WasTransferred(Ticket ticket, IReadOnlyDictionary<int, int?> payers)
        => RefundedTo(ticket, payers) is int payer && ticket.BuyerId is int holder && payer != holder;

    /// <summary>
    /// Báo người mua ban đầu khi vé họ đã chuyển nhượng được hoàn tiền — tiền về tài khoản của họ, nên họ
    /// phải là người biết.
    /// </summary>
    public static Task NotifyOriginalBuyerAsync(
        INotificationService notifications, Ticket ticket, IReadOnlyDictionary<int, int?> payers,
        NotificationType type, string showName, int showId, string why, CancellationToken ct)
    {
        if (!WasTransferred(ticket, payers) || RefundedTo(ticket, payers) is not int payer)
            return Task.CompletedTask;

        return notifications.NotifyAsync(
            payer,
            type,
            "Vé bạn đã chuyển nhượng được hoàn tiền",
            $"Vé \"{showName}\" bạn đã mua rồi chuyển nhượng cho người khác được hoàn tiền ({why}). Chúng tôi " +
            "đã tự động tạo yêu cầu hoàn 100% về phương thức bạn đã thanh toán — bạn không cần làm gì thêm.",
            referenceType: "show",
            referenceId: showId.ToString(),
            ct: ct);
    }
}
