namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record PosterGenerationAttemptDto(
    Guid Id,
    string Status,
    string? ImageUrl,
    string? ErrorMessage,
    DateTimeOffset CreatedAt);
