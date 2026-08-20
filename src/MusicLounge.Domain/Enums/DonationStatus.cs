namespace MusicLounge.Domain.Enums;

public enum DonationStatus
{
    PendingPayment,     // Audience khởi tạo — chờ VNPay confirm
    PendingOwnerAck,    // VNPay success — chờ Owner xác nhận
    OwnerReceived,      // Owner đã xác nhận nhận tiền
    PerformerPaid,      // Owner đã xác nhận trả nghệ sĩ
    Cancelled           // VNPay failed hoặc expired
}
