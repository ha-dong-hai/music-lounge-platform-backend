namespace MusicLounge.Application.LoungeShows.DTOs;

// Chi gom zone co it nhat 1 TicketTier trong show nay (khong tra ve toan bo zone cua venue —
// venue co the co 5 zone nhung show chi ban 2 zone thi ban do chi hien 2, giong TicketBox that).
public sealed record SeatingMapDto(
    string? AreaLayoutImageUrl,
    IReadOnlyList<ZoneMapEntryDto> Zones);

public sealed record ZoneMapEntryDto(
    int ZoneId,
    string Name,
    int Capacity,
    string? Color,
    double? Layout2DX,
    double? Layout2DY,
    double? Layout2DWidth,
    double? Layout2DHeight,
    double? Layout2DRotationDeg,
    double? Layout3DX,
    double? Layout3DY,
    double? Layout3DZ,
    int? AvailableCount, // null = khong gioi han (co it nhat 1 gia trong zone khong dat quota)
    decimal? MinPrice,
    decimal? MaxPrice);
