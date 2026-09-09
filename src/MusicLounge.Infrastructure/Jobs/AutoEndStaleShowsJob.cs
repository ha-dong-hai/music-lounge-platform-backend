using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Closes shows that have factually finished but were never marked Ended.
///
/// Nothing else in the system does this. A livestream show gets ended by its own end/terminate/
/// webhook/timeout paths, but an OFFLINE show with no livestream only ever reaches Ended if the
/// Owner remembers to call EndLoungeShow — and when they don't, the show sits at Published forever.
/// That single stuck status quietly breaks four separate things:
///
///   - tickets stay cancellable indefinitely (CancelTicket's only hard stop is Status == Ended),
///     so a refund can land weeks after both settlement tranches have already paid the venue;
///   - the rating window never opens, because RatingOpenUntil is only set on the Ended transition;
///   - SettlementReleaseJob's D16 completion check keeps returning "can't judge, assume fine"
///     because ActualEnd is null, so Final30 auto-releases with no completion evidence at all;
///   - the show keeps advertising itself as on sale — HoldTicket accepts Published.
///
/// ActualEnd is set to the show's ScheduledEnd, not to the moment this job happened to notice.
/// Backdating it that way keeps the completion ratio and the rating window anchored to when the
/// show really finished, so a job that runs late (or is restored after an outage) does not hand the
/// venue a longer apparent show or the audience a fresher rating window than they earned.
/// ActualStart is deliberately left untouched: nobody confirmed the show started, and inventing a
/// start time would turn "we cannot judge completion" into a fabricated ratio.
/// </summary>
public sealed class AutoEndStaleShowsJob
{
    private const int DefaultGraceHours = 6;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly ILogger<AutoEndStaleShowsJob> _logger;

    public AutoEndStaleShowsJob(
        ApplicationDbContext ctx, ISystemConfigService config, ILogger<AutoEndStaleShowsJob> logger)
    {
        _ctx = ctx;
        _config = config;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var graceHours = await _config.GetIntAsync(ConfigKeys.ShowAutoEndGraceHours, DefaultGraceHours, ct);
        var ratingWindowDays = await _config.GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);

        // Filter by Status server-side and the date client-side — combining an enum equality with a
        // DateTimeOffset comparison in one query does not translate under the SQLite provider used
        // in tests (the same limitation documented across the other jobs in this folder).
        var candidates = await _ctx.LoungeShows
            .Where(s => s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing)
            .ToListAsync(ct);

        var stale = candidates
            .Where(s => (s.ScheduledEnd ?? s.ScheduledStart.AddHours(ShowSchedule.DefaultDurationHours)).AddHours(graceHours) < now)
            .ToList();

        if (stale.Count == 0) return;

        foreach (var show in stale)
        {
            var scheduledEnd = ShowSchedule.EffectiveEnd(show);
            var previousStatus = show.Status;   // TryMarkEnded overwrites it below
            if (!LoungeShowLifecycle.TryMarkEnded(show, scheduledEnd, ratingWindowDays))
                continue;

            _logger.LogWarning(
                "Auto-ended stale show — ShowId={ShowId} was still {PreviousStatus} {HoursLate} hours " +
                "after its scheduled end; the venue never closed it manually. At {At}",
                show.Id, previousStatus, (int)(now - scheduledEnd).TotalHours, now);
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
