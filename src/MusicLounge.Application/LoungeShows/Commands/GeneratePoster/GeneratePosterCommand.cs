using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Commands.GeneratePoster;

// INoTransactionCommand: when the Gemini call fails, the handler logs a Failed AiPosterGeneration
// row and then re-throws so the caller sees a real error — that log write must survive the throw,
// not get rolled back by TransactionBehavior's ambient ROLLBACK-on-exception, since it's the whole
// reason a failed attempt doesn't cost the Owner's quota is auditable. Each individual
// SaveChangesAsync call still gets EF Core's own implicit per-call transaction regardless.
public sealed record GeneratePosterCommand(
    int ShowId,
    string? StyleHint
) : ICommand<PosterGenerationResultDto>, INoTransactionCommand;
