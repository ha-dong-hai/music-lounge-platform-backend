namespace MusicLounge.Application.Lounges.DTOs;

public sealed record LoungeDetailDto(
    int Id,
    string Name,
    string? PrimaryImageUrl,
    string? Model3DUrl,
    string? AreaLayoutImageUrl,
    string Street,
    string Ward,
    string District,
    string City,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    int FollowerCount,
    int UpcomingShowCount,
    bool? IsFollowing,
    string? Description,
    string? AtmosphereName,
    IReadOnlyList<LoungeGalleryImageDto> GalleryImages);

public sealed record LoungeGalleryImageDto(int Id, string ImageUrl, string? Caption, int OrderIndex);
