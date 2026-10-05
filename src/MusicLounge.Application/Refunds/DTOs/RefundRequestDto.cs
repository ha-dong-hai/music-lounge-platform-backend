using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Refunds.DTOs;

public sealed record RefundRequestDto(
    Guid Id,
    Guid PaymentId,
    Guid? RequestedBy,
    string Reason,
    decimal AmountRequested,
    decimal? AmountApproved,
    decimal? RefundPercentage,
    RefundRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    // Khi nao nguoi mua duoc tra loi. Truoc day mot RefundRequest nam Pending vo han, khong SLA,
    // khong canh bao, va nguoi mua khong co cach nao biet nen cho bao lau — day la khoang trong
    // niem tin lon nhat trong ca luong tien. Tinh tu CreatedAt + refund_sla_hours (mac dinh 72h)
    // thay vi luu thanh cot rieng, nen doi cau hinh la ap dung ngay cho ca cac yeu cau dang cho.
    // Con null khi da xu ly xong — luc do ResolvedAt moi la thong tin dung.
    DateTimeOffset? ExpectedResolutionBy,
    // MLACP-387: VNPay khong con hoan duoc giao dich goc va nguoi mua chua dong y nhan bang chuyen khoan — frontend hien o
    // khai tai khoan. Cac truong Payout* la tai khoan chinh nguoi mua da khai (Admin can so day du de chuyen khoan).
    bool PayoutAccountRequired = false,
    string? PayoutBankName = null,
    string? PayoutAccountNumber = null,
    string? PayoutAccountHolder = null,
    DateTimeOffset? PayoutConsentAt = null)
{
    /// <summary>MLACP-672: tên người gửi yêu cầu (họ tên trên tài khoản) — thay cho mã người dùng.</summary>
    public string? RequesterName { get; init; }

    /// <summary>MLACP-672: buổi diễn của các vé trong khoản thanh toán này — thay cho mã thanh toán.</summary>
    public string? ShowName { get; init; }

    /// <summary>MLACP-672: số vé trong khoản thanh toán này.</summary>
    public int TicketCount { get; init; }
}
