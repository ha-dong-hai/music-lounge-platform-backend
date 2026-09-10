using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Kết quả xử lý một callback VNPay, đủ chi tiết để trả đúng mã RspCode.
///
/// <para>Trước MLACP-334 cả bốn luồng thanh toán đều trả <c>bool</c>, nên mọi thứ không phải thành
/// công đều thành <c>99 Unknown error</c>. Theo tài liệu VNPay, <c>99</c> là mã <b>retry được</b> —
/// nên một callback trùng lặp bình thường, một chữ ký giả mạo, hay một đơn không tồn tại đều khiến
/// VNPay gọi lại đủ 10 lần trong ~50 phút cho một thứ không bao giờ đổi kết quả.</para>
/// </summary>
public enum VnPayIpnOutcome
{
    /// <summary>Xác nhận thành công, đã cập nhật trạng thái.</summary>
    Confirmed,

    /// <summary>
    /// VNPay báo giao dịch thất bại và hệ thống đã ghi nhận đúng như vậy. Với VNPay đây vẫn là
    /// "cập nhật thành công" — không có gì để gọi lại.
    /// </summary>
    RecordedAsFailed,

    /// <summary>Callback trùng lặp của một bản ghi đã xử lý xong. Vô hại.</summary>
    AlreadyProcessed,

    /// <summary>
    /// VNPay báo THÀNH CÔNG cho một bản ghi đã bị đóng (Failed/Cancelled). Đây là tiền thật đã thu
    /// mà hệ thống không còn ghi nhận — xem <see cref="PaymentIncident"/>.
    /// </summary>
    ConfirmedTooLate,

    /// <summary>Không tìm thấy đơn theo <c>vnp_TxnRef</c>.</summary>
    OrderNotFound,

    /// <summary>Số tiền VNPay báo không khớp số đã ghi.</summary>
    AmountMismatch,

    /// <summary>Chữ ký không hợp lệ — callback giả mạo hoặc hỏng.</summary>
    InvalidSignature,

    /// <summary>
    /// Dữ liệu hệ thống không nhất quán (gói đăng ký hoặc đơn hàng mà thanh toán trỏ tới không còn).
    /// Giữ mã retry được vì không loại trừ được khả năng đây chỉ là trục trặc nhất thời.
    /// </summary>
    InternalError
}

/// <summary>
/// Dịch <see cref="VnPayIpnOutcome"/> sang hợp đồng VNPay, một chỗ duy nhất cho cả bốn controller
/// thay vì mỗi nơi chép lại một bảng map rồi lệch nhau.
/// </summary>
public static class VnPayIpnProtocol
{
    /// <summary>
    /// Mã VNPay đọc để quyết định có gọi lại hay không — nó đọc thân phản hồi, không đọc HTTP status.
    /// <c>00</c> và <c>02</c> kết thúc retry; <c>01</c>, <c>04</c>, <c>97</c>, <c>99</c> kích hoạt
    /// gọi lại tối đa 10 lần, mỗi lần cách 5 phút.
    /// </summary>
    public static (string RspCode, string Message) ResponseFor(VnPayIpnOutcome outcome) => outcome switch
    {
        VnPayIpnOutcome.Confirmed        => ("00", "Confirm Success"),
        VnPayIpnOutcome.RecordedAsFailed => ("00", "Confirm Success"),
        VnPayIpnOutcome.AlreadyProcessed => ("02", "Order already confirmed"),

        // Cũng dùng 02: gọi lại sẽ chỉ gặp đúng ngõ cụt đó thêm chín lần nữa, trong khi sự cố đã
        // được ghi nhận và đã có người được báo. Để VNPay retry ở đây chỉ nhân bản một dòng log
        // ERROR chứ không cứu được đồng nào.
        VnPayIpnOutcome.ConfirmedTooLate => ("02", "Order already confirmed"),

        VnPayIpnOutcome.OrderNotFound    => ("01", "Order not found"),
        VnPayIpnOutcome.AmountMismatch   => ("04", "Invalid amount"),
        VnPayIpnOutcome.InvalidSignature => ("97", "Invalid signature"),
        VnPayIpnOutcome.InternalError    => ("99", "Unknown error"),
        _                                => ("99", "Unknown error")
    };

    /// <summary>
    /// Trình duyệt khách quay về thì cho xem trang nào. <see cref="VnPayIpnOutcome.ConfirmedTooLate"/>
    /// cố ý KHÔNG tính là thành công: khách thật sự không có vé, nói ngược lại là nói dối họ. Trang
    /// đúng phải là trang thứ ba — "đã thu tiền nhưng chưa cấp được vé, đang xử lý" — nhưng
    /// <c>BusinessSettings</c> hiện chỉ có hai URL, nên cần frontend làm thêm.
    /// </summary>
    public static bool IsBuyerFacingSuccess(VnPayIpnOutcome outcome)
        => outcome is VnPayIpnOutcome.Confirmed or VnPayIpnOutcome.AlreadyProcessed;

    /// <summary>
    /// MLACP-344: trang trinh duyet cua khach duoc dua toi, cho ca bon luong thanh toan.
    ///
    /// <para>Ba ket cuc chu khong phai hai. <see cref="VnPayIpnOutcome.ConfirmedTooLate"/> nghia la
    /// khach DA tra tien ma chua duoc cap gi — trang thanh cong la noi doi, trang that bai cung sai
    /// vi tien da roi khoi tai khoan, va khach thay "that bai" co the mua lai roi bi tru hai lan.</para>
    ///
    /// <para>Chua cau hinh <c>PaymentProcessingUrl</c> thi quay ve trang that bai nhu cu, de khong
    /// lam vo moi truong dang chay.</para>
    /// </summary>
    public static string BuyerLandingUrl(VnPayIpnOutcome outcome, BusinessSettings settings)
    {
        if (IsBuyerFacingSuccess(outcome))
            return settings.PaymentSuccessUrl;

        if (outcome == VnPayIpnOutcome.ConfirmedTooLate
            && !string.IsNullOrWhiteSpace(settings.PaymentProcessingUrl))
            return settings.PaymentProcessingUrl;

        return settings.PaymentFailedUrl;
    }
}

/// <summary>
/// Ghi nhận trường hợp VNPay xác nhận thành công cho một bản ghi đã đóng.
///
/// <para>Cố ý KHÔNG tự khôi phục. Cấp lại vé ngay trong callback có thể làm vượt sức chứa thật của
/// phòng trà — chỗ đã được trả lại kho và có thể đã bán cho người khác. Còn quyết định hoàn tiền
/// thuộc chính sách, không thuộc một handler callback. Việc duy nhất đúng ở tầng này là **đảm bảo
/// sự cố không biến mất**: ghi ERROR với đủ dữ liệu đối soát, và báo cho người có thể xử lý.</para>
/// </summary>
public static class PaymentIncident
{
    public static async Task RecordConfirmedTooLateAsync(
        IUnitOfWork uow,
        INotificationService notifications,
        ILogger logger,
        string what,
        string? txnRef,
        decimal amount,
        string referenceType,
        string referenceId,
        CancellationToken ct)
    {
        logger.LogError(
            "VNPay xac nhan THANH CONG cho mot ban ghi da dong — tien da thu ma he thong khong con " +
            "ghi nhan. Loai={What} TxnRef={TxnRef} SoTien={Amount} Ref={ReferenceType}/{ReferenceId} at {At}",
            what, txnRef, amount, referenceType, referenceId, DateTimeOffset.UtcNow);

        var admins = await uow.Repository<User, int>().FindAsync(u => u.Role == UserRole.Admin, ct);
        if (admins.Count == 0) return;

        foreach (var admin in admins)
        {
            await notifications.NotifyAsync(
                admin.Id,
                NotificationType.PaymentConfirmedAfterExpiry,
                "Thanh toán được xác nhận sau khi đơn đã đóng",
                $"VNPay báo thành công {amount:N0}đ cho {what} (mã giao dịch {txnRef}), nhưng bản ghi " +
                "đã bị đóng trước đó nên hệ thống không cấp được gì. Cần đối soát với VNPay rồi " +
                "cấp lại hoặc hoàn tiền cho khách.",
                referenceType: referenceType,
                referenceId: referenceId,
                ct: ct);
        }

        // NotifyAsync chỉ mới Add vào change tracker — không lưu ở đây thì cảnh báo bay mất, và đây
        // là nhánh không thay đổi gì khác nên lưu ở đây là an toàn.
        await uow.SaveChangesAsync(ct);
    }
}
