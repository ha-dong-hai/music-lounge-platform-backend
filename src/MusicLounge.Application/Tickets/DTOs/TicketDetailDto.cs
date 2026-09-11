using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DTOs;

public sealed record TicketDetailDto(
    Guid Id,
    string ShowName,
    string LoungeName,
    string LoungeAddress,
    DateTimeOffset ShowScheduledStart,
    DateTimeOffset? ShowScheduledEnd,
    string TierName,
    string PriceName,
    decimal PricePaid,
    AccessType AccessType,
    TicketStatus Status,
    string? QrCode,
    DateTimeOffset PurchasedAt,
    PhysicalDetailDto? PhysicalDetail,
    TicketLivestreamDetailDto? LivestreamDetail,
    // MLACP-372: phòng trà đổi lịch / địa chỉ sau khi vé được mua → huỷ được và hoàn 100% tới mốc này, bất kể chính
    // sách của buổi diễn. Null khi vé không chịu thay đổi nào (và ở các màn của nhân viên). Mốc có thể đã qua.
    DateTimeOffset? FullRefundUntil = null);

public sealed record PhysicalDetailDto(
    string? SeatInfo,
    DateTimeOffset? CheckedInAt);

public sealed record TicketLivestreamDetailDto(
    string? AccessToken);
