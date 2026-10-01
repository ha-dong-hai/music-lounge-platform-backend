using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;

public sealed record PerformanceInput(
    Guid? PerformerId,
    string? PerformerName,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation);

public sealed record CreateLoungeShowCommand(
    Guid LoungeId,
    string Name,
    string Description,
    string Format,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    DateTimeOffset? TicketSaleClosesAt,
    Guid? CategoryId,
    int? OfflineQuota,
    int? OnlineQuota,
    IReadOnlyList<Guid> GenreIds,
    IReadOnlyList<Guid> MoodIds,
    IReadOnlyList<Guid> AtmosphereIds,
    IReadOnlyList<PerformanceInput> Performances,
    // MLACP-288. The three D13 policy columns, finally settable. All optional: omitting them keeps
    // the platform default every existing show already runs on — cancellable, 100% refund, no
    // deadline. Whatever is chosen here is what GetLoungeShowDetail publishes to the buyer before
    // they pay and what CancelTicket enforces afterwards; the two read the same resolver.
    bool? CancellationAllowed = null,
    decimal? RefundPercentage = null,
    int? CancellationDeadlineHours = null
) : ICommand<Guid>;
