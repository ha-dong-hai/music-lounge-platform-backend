namespace MusicLounge.Application.Notifications;

/// <summary>
/// MLACP-455. Từ vựng của <c>Notification.ReferenceType</c> — thứ frontend dùng để biết bấm vào thông báo thì mở màn hình
/// nào, và <c>ReferenceId</c> khi đó là mã của cái gì.
///
/// <para>Trước task này có 21 giá trị với ba kiểu viết lẫn lộn (<c>venue_penalty</c>, <c>fnbOrder</c>, <c>kyc-review</c>),
/// và <c>refund</c> lẫn <c>refund_request</c> cùng trỏ vào <c>RefundRequest.Id</c> — dùng lẫn nhau ngay trong một file.
/// Nay: chỉ snake_case, một tên cho một tài nguyên.</para>
///
/// <para><b>Không nhầm với <c>Payment.ReferenceType</c></b> (<c>TicketHold</c>, <c>WalkIn</c>, <c>Donation</c>,
/// <c>Subscription</c>, <c>FnbOrder</c>) — đó là từ vựng khác, nói một khoản thanh toán sinh ra từ đâu.</para>
///
/// <para>Chỗ gọi vẫn truyền chuỗi (đọc liền mạch cùng tiêu đề/nội dung thông báo ngay tại đó); test
/// <c>NotificationReferenceTypeVocabularyTests</c> quét mã nguồn và bắt buộc mọi chuỗi phải nằm trong danh sách này.</para>
/// </summary>
public static class NotificationReferenceTypes
{
    /// <summary>Buổi hòa nhạc — <c>LoungeShow.Id</c>.</summary>
    public const string Show = "show";

    /// <summary>Phòng trà — <c>MusicLounge.Id</c>.</summary>
    public const string Lounge = "lounge";

    /// <summary>Vé — <c>Ticket.Id</c> (GUID).</summary>
    public const string Ticket = "ticket";

    /// <summary>
    /// Kết quả duyệt buổi phát trực tiếp — mã đi kèm là <c>LoungeShow.Id</c> (MLACP-460), KHÔNG phải <c>Livestream.Id</c>.
    ///
    /// Trước đây trả mã buổi phát, mà mọi đường dẫn của frontend đều nhận mã buổi hòa nhạc — nên bấm vào thông báo hoặc
    /// mở nhầm buổi khác (hai bảng đánh số riêng nên mã dễ trùng số), hoặc ra trang trống. Giữ tên loại là
    /// <c>livestream</c> để frontend vẫn phân biệt được đây là kết quả duyệt buổi PHÁT, không phải duyệt buổi hòa nhạc.
    /// </summary>
    public const string Livestream = "livestream";

    /// <summary>Giao dịch thanh toán — <c>Payment.Id</c>.</summary>
    public const string Payment = "payment";

    /// <summary>Yêu cầu hoàn tiền — <c>RefundRequest.Id</c>. Gộp từ tên cũ <c>refund</c>.</summary>
    public const string RefundRequest = "refund_request";

    /// <summary>
    /// Cảnh báo hoàn tiền MẶT chưa trao tay — cũng là <c>RefundRequest.Id</c>, nhưng cố ý giữ tên riêng: nó là khoá chống
    /// gửi trùng của <c>RefundSlaBreachAlertJob.NotifyOnceAsync</c>, mà hàm đó không lọc theo tiêu đề. Gộp vào
    /// <see cref="RefundRequest"/> sẽ khiến cảnh báo tiền mặt bị nuốt vì trùng khoá với cảnh báo quá hạn (cùng người,
    /// cùng loại thông báo, cùng mã yêu cầu).
    /// </summary>
    public const string CashRefund = "cash_refund";

    /// <summary>Khoản quyết toán — <c>Settlement.Id</c>.</summary>
    public const string Settlement = "settlement";

    /// <summary>Nhắc chủ phòng trà khai tài khoản nhận chi trả — <c>User.Id</c> của chủ.</summary>
    public const string PayoutOwner = "payout_owner";

    /// <summary>Tài khoản ngân hàng — <c>BankAccount.Id</c>.</summary>
    public const string BankAccount = "bank_account";

    /// <summary>Lượt donate — <c>Donation.Id</c>.</summary>
    public const string Donation = "donation";

    /// <summary>Đơn gọi món — <c>FnbOrder.Id</c>. Tên cũ: <c>fnbOrder</c>.</summary>
    public const string FnbOrder = "fnb_order";

    /// <summary>Gói dịch vụ của chủ phòng trà — <c>OwnerSubscription.Id</c>.</summary>
    public const string Subscription = "subscription";

    /// <summary>Khiếu nại — <c>Complaint.Id</c>.</summary>
    public const string Complaint = "complaint";

    /// <summary>Án phạt phòng trà — <c>VenuePenalty.Id</c>.</summary>
    public const string VenuePenalty = "venue_penalty";

    /// <summary>Hồ sơ kiểm duyệt nội dung — <c>EventModeration.Id</c>.</summary>
    public const string EventModeration = "event_moderation";

    /// <summary>Nội dung bị báo cáo — chuỗi ghép <c>"{TargetType}:{TargetId}"</c>, không phải một mã đơn.</summary>
    public const string ContentReportTarget = "content_report_target";

    /// <summary>Hồ sơ định danh (CCCD/giấy phép) — <c>User.Id</c>. Tên cũ: <c>kyc-review</c>.</summary>
    public const string KycReview = "kyc_review";

    /// <summary>Người dùng — <c>User.Id</c>.</summary>
    public const string User = "user";

    /// <summary>Cảnh báo an ninh theo địa chỉ IP — <c>ReferenceId</c> là chính địa chỉ IP, không phải mã bản ghi.</summary>
    public const string SecurityIp = "security_ip";
}
