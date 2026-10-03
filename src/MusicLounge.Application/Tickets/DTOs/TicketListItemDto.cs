using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DTOs;

public sealed record TicketListItemDto(
    Guid Id,
    Guid ShowId,
    string ShowName,
    string LoungeName,
    string LoungeCity,
    DateTimeOffset ShowScheduledStart,
    string TierName,
    string PriceName,
    decimal PricePaid,
    AccessType AccessType,
    TicketStatus Status,
    string? QrCode,
    DateTimeOffset PurchasedAt,
    bool HasPendingTransfer,
    // MLACP-546: trang Vé của tôi in "kỷ niệm đêm đã đến". AttendedAt = lúc khách THẬT SỰ có mặt: vé tại chỗ là giờ quét
    // vào cửa (PhysicalTicketDetail.CheckedInAt), vé trực tuyến là lần đầu vào xem (LivestreamTicketDetail.FirstAccessedAt —
    // "vào phiên phát = đã tham dự"). null = chưa đến. ShowImageUrl = ảnh bìa buổi, chưa có thì ảnh phòng trà (cùng quy tắc
    // đầu trang buổi diễn). Có sẵn trong danh sách để giao diện không phải gọi chi tiết từng vé (N+1).
    DateTimeOffset? AttendedAt = null,
    string? ShowImageUrl = null);
