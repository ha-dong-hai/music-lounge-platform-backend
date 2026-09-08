namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record LoungeSummaryDto(
    int Id,
    string Name,
    string Street,
    string Ward,
    string District,
    string City,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    string? PrimaryImageUrl,
    string? Model3DUrl,
    string? AtmosphereName,
    IReadOnlyList<LoungeGalleryImageDto> GalleryImages);

public sealed record LoungeGalleryImageDto(int Id, string ImageUrl, string? Caption);
