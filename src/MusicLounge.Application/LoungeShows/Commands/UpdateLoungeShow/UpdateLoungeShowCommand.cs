using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;

public sealed record UpdateLoungeShowCommand(
    int ShowId,
    string Name,
    string Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    DateTimeOffset? TicketSaleClosesAt,
    int? CategoryId,
    int? OfflineQuota,
    int? OnlineQuota,
    // MLACP-288. The three D13 policy columns, finally settable. All optional: omitting them keeps
    // the platform default every existing show already runs on — cancellable, 100% refund, no
    // deadline. Whatever is chosen here is what GetLoungeShowDetail publishes to the buyer before
    // they pay and what CancelTicket enforces afterwards; the two read the same resolver.
    bool? CancellationAllowed = null,
    decimal? RefundPercentage = null,
    int? CancellationDeadlineHours = null
) : ICommand;
