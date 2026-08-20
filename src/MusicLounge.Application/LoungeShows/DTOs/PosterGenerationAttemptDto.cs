namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record PosterGenerationAttemptDto(
    int Id,
    string Status,
    string? ImageUrl,
    string? ErrorMessage,
    DateTimeOffset CreatedAt);
