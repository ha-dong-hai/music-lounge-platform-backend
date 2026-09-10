using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.FnbOrders;

/// <summary>
/// MLACP-349 — "đơn F&amp;B đã trả tiền chưa" là một câu hỏi về <b>tiền</b>, không phải về bếp.
///
/// <para><b>Trước đây</b> hai câu hỏi đó dùng chung một trường: <c>FnbOrder.Status == Paid</c>. IPN
/// VNPay nhảy thẳng đơn trả trước từ <c>Pending</c> sang <c>Paid</c>, mà trong chuỗi
/// Pending → Preparing → Served → Paid không còn bước nào sau <c>Paid</c> — nên bếp không chuyển được
/// đơn đó sang Preparing hay Served nữa. Và vì IPN chỉ chống trùng theo từng <c>Payment</c>, một đơn
/// có thể được "trả" lần thứ hai mà không ai chặn.</para>
///
/// <para><b>Nay</b> bảng <c>payments</c> là nguồn sự thật: một đơn đã trả tiền khi có một thanh toán
/// <c>Confirmed</c> cho nó (online qua VNPay, hoặc tiền mặt do nhân viên ghi), hoặc khi nó đã được
/// đóng ở <c>Paid</c> — điều kiện sau giữ đúng dữ liệu cũ, từ trước khi đường tiền mặt bắt đầu ghi
/// bản ghi <c>Payment</c>. <c>Status</c> chỉ còn nói bếp đã làm tới đâu; <c>Paid</c> là "đã phục vụ
/// xong và đã trả tiền — đóng đơn".</para>
///
/// <para>Cơ sở: VNPay yêu cầu kiểm trạng thái <b>đơn hàng</b> trong IPN — "Việc kiểm tra trạng thái
/// của đơn hàng giúp hệ thống không xử lý trùng lặp, xử lý nhiều lần một giao dịch"; Square không cho
/// hoàn tất phần phục vụ trước khi đơn được thanh toán ("You cannot set the fulfillment.state of any
/// order fulfillment to COMPLETED until after you call PayOrder").</para>
/// </summary>
public static class FnbOrderPayments
{
    public const string ReferenceType = "FnbOrder";

    /// <summary>
    /// Mot khoa cho moi thao tac doi trang thai tien cua don: bat dau thanh toan online, IPN xac
    /// nhan, nhan vien thu tien mat hay huy. Thieu no thi IPN va nhan vien co the cung doc "chua tra"
    /// roi cung ghi nhan mot khoan thu.
    /// </summary>
    public static string LockKey(int orderId) => $"fnb-order:{orderId}";

    /// <summary>Những đơn trong tập này đã có một thanh toán được xác nhận.</summary>
    public static async Task<HashSet<int>> ConfirmedOrderIdsAsync(
        IUnitOfWork uow, IReadOnlyCollection<int> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return [];

        var referenceIds = orderIds.Select(id => id.ToString()).Distinct().ToList();
        var payments = await uow.Repository<Payment, int>().FindAsync(
            p => p.ReferenceType == ReferenceType
                 && p.Status == PaymentStatus.Confirmed
                 && referenceIds.Contains(p.ReferenceId), ct);

        return payments.Select(p => int.Parse(p.ReferenceId)).ToHashSet();
    }

    /// <summary>
    /// Đơn này đã có thanh toán được xác nhận chưa — không tính <paramref name="exceptPaymentId"/>,
    /// để IPN hỏi được "có thanh toán NÀO KHÁC đã trả cho đơn này chưa".
    /// </summary>
    public static async Task<bool> HasConfirmedPaymentAsync(
        IUnitOfWork uow, int orderId, int? exceptPaymentId, CancellationToken ct)
    {
        var referenceId = orderId.ToString();
        return await uow.Repository<Payment, int>().AnyAsync(
            p => p.ReferenceType == ReferenceType
                 && p.ReferenceId == referenceId
                 && p.Status == PaymentStatus.Confirmed
                 && (exceptPaymentId == null || p.Id != exceptPaymentId), ct);
    }

    public static bool IsPaid(FnbOrder order, bool hasConfirmedPayment)
        => order.Status == FnbOrderStatus.Paid || hasConfirmedPayment;

    /// <summary>
    /// Giao dịch online khách còn có thể hoàn tất: đang chờ, qua cổng thanh toán, và link VNPay chưa
    /// hết hạn (<see cref="VnPayPaymentWindow.Minutes"/>). Trong khoảng này mà nhân viên thu tiền mặt
    /// hay huỷ đơn thì khách vẫn có thể bấm trả tiếp trên VNPay — và bị trừ tiền cho một đơn đã
    /// trả hoặc đã huỷ.
    /// </summary>
    public static async Task<Payment?> LiveOnlinePaymentAsync(
        IUnitOfWork uow, int orderId, DateTimeOffset now, CancellationToken ct)
    {
        var referenceId = orderId.ToString();

        // Lọc trạng thái phía server, so thời gian phía client — provider SQLite dùng trong test không
        // dịch được phép so enum kèm DateTimeOffset trong cùng một truy vấn.
        var pending = await uow.Repository<Payment, int>().FindAsync(
            p => p.ReferenceType == ReferenceType
                 && p.ReferenceId == referenceId
                 && p.Status == PaymentStatus.Pending
                 && p.Method == PaymentMethod.Gateway, ct);

        return pending
            .Where(p => p.CreatedAt.AddMinutes(VnPayPaymentWindow.Minutes) > now)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Với mỗi đơn đang có giao dịch online còn trả được, thời điểm link VNPay hết hạn — để màn hình
    /// của nhân viên thấy "khách đang trả online" trước khi họ thu tiền mặt.
    /// </summary>
    public static async Task<Dictionary<int, DateTimeOffset>> LiveOnlinePaymentDeadlinesAsync(
        IUnitOfWork uow, IReadOnlyCollection<int> orderIds, DateTimeOffset now, CancellationToken ct)
    {
        if (orderIds.Count == 0) return [];

        var referenceIds = orderIds.Select(id => id.ToString()).Distinct().ToList();
        var pending = await uow.Repository<Payment, int>().FindAsync(
            p => p.ReferenceType == ReferenceType
                 && p.Status == PaymentStatus.Pending
                 && p.Method == PaymentMethod.Gateway
                 && referenceIds.Contains(p.ReferenceId), ct);

        return pending
            .Select(p => (OrderId: int.Parse(p.ReferenceId),
                          Deadline: p.CreatedAt.AddMinutes(VnPayPaymentWindow.Minutes)))
            .Where(x => x.Deadline > now)
            .GroupBy(x => x.OrderId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Deadline));
    }

    /// <summary>Số phút (làm tròn lên) còn lại trước khi link VNPay của giao dịch này hết hạn.</summary>
    public static int MinutesLeft(Payment livePayment, DateTimeOffset now)
        => Math.Max(1, (int)Math.Ceiling(
            (livePayment.CreatedAt.AddMinutes(VnPayPaymentWindow.Minutes) - now).TotalMinutes));
}
