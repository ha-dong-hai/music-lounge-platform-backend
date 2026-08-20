namespace MusicLounge.Application.Lounges.DTOs;

public sealed record LoungeListItemDto(
    int Id,
    string Name,
    string? PrimaryImageUrl,
    string? BusinessLicenseUrl,
    string? Model3DUrl,
    string? AreaLayoutImageUrl,
    string Street,
    string District,
    string City,
    int FollowerCount,
    int UpcomingShowCount);
