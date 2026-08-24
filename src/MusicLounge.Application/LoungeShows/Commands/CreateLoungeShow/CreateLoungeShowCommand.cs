using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;

public sealed record PerformanceInput(
    int? PerformerId,
    string? PerformerName,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation);

public sealed record CreateLoungeShowCommand(
    int LoungeId,
    string Name,
    string Description,
    string Format,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    int? CategoryId,
    int? OfflineQuota,
    int? OnlineQuota,
    IReadOnlyList<int> GenreIds,
    IReadOnlyList<int> MoodIds,
    IReadOnlyList<int> AtmosphereIds,
    IReadOnlyList<PerformanceInput> Performances
) : ICommand<int>;
