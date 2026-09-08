using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Refunds.DTOs;

public sealed record RefundRequestDto(
    int Id,
    int PaymentId,
    int? RequestedBy,
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
    DateTimeOffset? ExpectedResolutionBy);
