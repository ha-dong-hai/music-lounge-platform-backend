namespace MusicLounge.Domain.Enums;

/// <summary>MLACP-363 — các bước của một khoản donate được ghi vào nhật ký bằng chứng.</summary>
public enum DonationEventType
{
    PaymentConfirmed,       // VNPay xác nhận khán giả đã trả tiền
    PayoutReleased,         // nền tảng chuyển phần của phòng trà (chặng 1)
    VenueAcknowledged,      // chủ phòng trà xác nhận đã nhận
    VenueAutoAcknowledged,  // hệ thống tự xác nhận khi quá hạn
    VenueReportedPaid,      // chủ phòng trà báo đã chuyển cho nghệ sĩ (chặng 2)
    MessageHidden,          // lời nhắn bị gỡ khỏi livestream
    PerformerConfirmedReceipt, // MLACP-364: nghệ sĩ xác nhận đã nhận (liên kết một lần)
    PerformerDisputedReceipt   // MLACP-364: nghệ sĩ báo chưa nhận
}
