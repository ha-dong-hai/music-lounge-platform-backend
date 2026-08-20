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
    TicketLivestreamDetailDto? LivestreamDetail);

public sealed record PhysicalDetailDto(
    string? SeatInfo,
    DateTimeOffset? CheckedInAt);

public sealed record TicketLivestreamDetailDto(
    string? AccessToken);
