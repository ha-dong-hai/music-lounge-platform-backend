using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetPosterGenerationHistory;

public sealed record GetPosterGenerationHistoryQuery(int ShowId) : IQuery<IReadOnlyList<PosterGenerationAttemptDto>>;
