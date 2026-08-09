using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;

// INoTransactionCommand: same reasoning as GeneratePosterCommand — when the stitcher call fails,
// the handler logs a Failed VenueTourStitchAttempt row and re-throws so the caller sees a real
// error. That log write must survive the throw, not get rolled back by TransactionBehavior's
// ambient ROLLBACK-on-exception, since the anti-abuse cap (tour_stitch_max_attempts_per_lounge)
// only works if failed attempts actually persist.
public sealed record StitchVenueTourSceneCommand(
    int LoungeId,
    IReadOnlyList<string> SourceImageUrls,
    string? Name
) : ICommand<int>, INoTransactionCommand;
