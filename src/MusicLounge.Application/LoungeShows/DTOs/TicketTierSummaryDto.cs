using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record TicketTierSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    AccessType AccessType,
    int? TotalCapacity,
    Guid? ZoneId,
    IReadOnlyList<TicketPriceSummaryDto> Prices);
