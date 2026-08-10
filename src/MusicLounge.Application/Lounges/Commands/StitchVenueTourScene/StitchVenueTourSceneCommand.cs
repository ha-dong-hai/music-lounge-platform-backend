using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;

// INoTransactionCommand: the handler creates a Pending VenueTourStitchAttempt row, calls
// SaveChangesAsync, then immediately enqueues StitchVenueTourSceneJob. Without this marker,
// TransactionBehavior wraps the whole handler in one ambient transaction that only commits AFTER
// Handle() returns — but Hangfire's SQL Server storage polls near-instantly
// (QueuePollInterval = TimeSpan.Zero), so the job could start and query for this attempt row
// BEFORE the outer transaction commits, finding nothing. Without the ambient transaction, the
// SaveChangesAsync call above commits immediately via EF's own implicit transaction, so the row
// is guaranteed visible by the time the job can possibly run.
public sealed record StitchVenueTourSceneCommand(
    int LoungeId,
    IReadOnlyList<string> SourceImageUrls,
    string? Name
) : ICommand<int>, INoTransactionCommand;
