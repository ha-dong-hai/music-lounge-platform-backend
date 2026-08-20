using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record TicketTierSummaryDto(
    int Id,
    string Name,
    string? Description,
    AccessType AccessType,
    int? TotalCapacity,
    int? ZoneId,
    IReadOnlyList<TicketPriceSummaryDto> Prices);
