namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record PosterGenerationResultDto(
    string ImageUrl,
    int RemainingThisMonth);
