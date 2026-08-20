namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record FilterOptionsDto(
    IReadOnlyList<GenreDto> Genres,
    IReadOnlyList<MoodDto> Moods,
    IReadOnlyList<AtmosphereDto> Atmospheres,
    IReadOnlyList<string> Cities);

public sealed record MoodDto(int Id, string Name);
public sealed record AtmosphereDto(int Id, string Name);
