namespace MusicLounge.Application.Users.DTOs;

public sealed record UserProfileDto(
    int Id,
    string FullName,
    string Email,
    string? AvatarUrl,
    bool AiConsent,
    IReadOnlyList<int> FavouriteGenreIds,
    IReadOnlyList<int> FavouriteMoodIds,
    IReadOnlyList<int> FavouriteAtmosphereIds);
