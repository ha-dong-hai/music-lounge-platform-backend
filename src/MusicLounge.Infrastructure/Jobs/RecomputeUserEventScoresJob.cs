using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// Populates user_event_scores (the collaborative-filtering training matrix MLNetRecommendationService
// reads from) — previously NEVER written anywhere in the codebase, so EnsureCollabModelTrainedAsync's
// "need >=10 rows" guard always failed and collab_score was always 0 for every user, silently
// disabling 30% of the documented hybrid formula (content*0.5 + collab*0.3 + custom*0.2) since the
// day it was written. This job aggregates every real behavioural/transactional signal this system
// already has into one weighted score per (user, show) pair.
//
// Weights are grounded in standard implicit-feedback recommender practice (explicit
// purchase/attendance > explicit rating > money spent > explicit stated interest > passive
// viewing — see "Weighting Different Signals" in recommender-systems literature, 2026) and mirror
// the field order UserEventScore.Breakdown's own doc comment already specified:
// {attended, rating, donated, wishlist, view}. clickIntent is a new field (ClickTicket/
// WatchLivestream/ShareEvent) — those BehaviourAction values existed in the enum but had no bucket
// in the original 5-key breakdown; placed between wishlist and view since "started checking out" or
// "actually watched the stream" is stronger signal than a passive page view but not yet a
// completed transaction.
public sealed class RecomputeUserEventScoresJob
{
    private const float AttendedWeight = 10f;
    private const float MaxRatingWeight = 8f;      // scaled by stars/5
    private const float DonatedWeight = 6f;
    private const float WishlistWeight = 4f;
    private const int MaxIntentCount = 3;
    private const float IntentWeightPerAction = 2f; // capped at MaxIntentCount distinct actions
    private const int MaxViewCount = 3;
    private const float ViewWeightPerAction = 1f;   // capped so idle refreshing can't dominate

    private static readonly BehaviourAction[] ViewFamily =
    [
        BehaviourAction.ViewEvent, BehaviourAction.ViewEventLong,
        BehaviourAction.ViewAfterWishlist, BehaviourAction.ViewLineup, BehaviourAction.ViewVenue
    ];

    private static readonly BehaviourAction[] IntentFamily =
    [
        BehaviourAction.ClickTicket, BehaviourAction.WatchLivestream, BehaviourAction.ShareEvent
    ];

    private readonly ApplicationDbContext _ctx;

    public RecomputeUserEventScoresJob(ApplicationDbContext ctx) => _ctx = ctx;

    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var scores = new Dictionary<(int UserId, int ShowId), ScoreAccumulator>();

        await AccumulateViewsAsync(scores, ct);
        await AccumulateIntentAsync(scores, ct);
        await AccumulateWishlistAsync(scores, ct);
        await AccumulateAttendanceAsync(scores, ct);
        await AccumulateDonationsAsync(scores, ct);
        await AccumulateRatingsAsync(scores, ct);

        // MLACP-318. Chi lap ho so cho nguoi da dong y.
        //
        // Sau nguon gop o tren, chi hai nguon dau (luot xem, bam mua) doc tu nhat ky hanh vi va vi
        // the da bi chan boi consent. Bon nguon con lai — luu quan tam, di xem, donate, danh gia —
        // doc thang tu giao dich, ton tai bat ke nguoi dung co dong y hay khong. Ket qua la job nay
        // dung ho so hanh vi theo tung buoi dien cho CA nhung nguoi chua bao gio dong y, roi ho so
        // do di thang vao tap huan luyen cua mo hinh loc cong tac.
        //
        // Dem tong hop tren giao dich thi khong can xin phep — do la dem, khong phai lap ho so.
        // Nhung bang nay khoa theo (nguoi, buoi dien) va duoc dung de suy ra so thich, tuc dung la
        // lap ho so. Ranh gioi nam o do.
        var consentingUserIds = (await _ctx.Users
                .Where(u => u.AiConsent)
                .Select(u => u.Id)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var key in scores.Keys.Where(k => !consentingUserIds.Contains(k.UserId)).ToList())
            scores.Remove(key);

        // Whole table, not a per-key filtered query — a composite-key "IN this set of (user,show)
        // pairs" doesn't translate reliably across providers, and this table stays small enough
        // (one row per user×show that ever had ANY interaction) for a nightly job to load in full,
        // same "load fully, merge in memory" style MLNetRecommendationService itself already uses.
        var existing = await _ctx.Set<UserEventScore>().ToListAsync(ct);

        // Don cac dong cua nguoi khong con dong y. Truoc day job nay chi upsert, khong bao gio xoa,
        // nen mot nguoi rut lai su dong y van de lai ho so cua minh nam do vinh vien — va no van
        // duoc dung. Day cung la luoi do thu hai cho duong rut lai dong y: neu vi ly do gi do buoc
        // xoa ngay luc bam khong chay, dem hom sau job nay van don sach.
        var toRemove = existing.Where(r => !consentingUserIds.Contains(r.UserId)).ToList();
        if (toRemove.Count > 0) _ctx.Set<UserEventScore>().RemoveRange(toRemove);

        if (scores.Count == 0)
        {
            if (toRemove.Count > 0) await _ctx.SaveChangesAsync(ct);
            return;
        }

        var existingByKey = existing
            .Where(r => consentingUserIds.Contains(r.UserId))
            .ToDictionary(r => (r.UserId, r.ShowId));

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, acc) in scores)
        {
            var breakdown = JsonSerializer.Serialize(new
            {
                attended = acc.Attended,
                rating = acc.RatingStars,
                donated = acc.Donated,
                wishlist = acc.Wishlisted,
                clickIntent = acc.IntentCount,
                view = acc.ViewCount
            });

            if (existingByKey.TryGetValue(key, out var row))
            {
                row.Score = (decimal)acc.TotalScore;
                row.Breakdown = breakdown;
                row.ComputedAt = now;
            }
            else
            {
                _ctx.Set<UserEventScore>().Add(new UserEventScore
                {
                    UserId = key.UserId,
                    ShowId = key.ShowId,
                    Score = (decimal)acc.TotalScore,
                    Breakdown = breakdown,
                    ComputedAt = now
                });
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private async Task AccumulateViewsAsync(
        Dictionary<(int, int), ScoreAccumulator> scores, CancellationToken ct)
    {
        var rows = await _ctx.BehaviourLogs
            .Where(l => ViewFamily.Contains(l.Action))
            .GroupBy(l => new { l.UserId, l.LoungeShowId })
            .Select(g => new { g.Key.UserId, g.Key.LoungeShowId, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var r in rows)
            Get(scores, r.UserId, r.LoungeShowId).ViewCount = r.Count;
    }

    private async Task AccumulateIntentAsync(
        Dictionary<(int, int), ScoreAccumulator> scores, CancellationToken ct)
    {
        var rows = await _ctx.BehaviourLogs
            .Where(l => IntentFamily.Contains(l.Action))
            .GroupBy(l => new { l.UserId, l.LoungeShowId })
            .Select(g => new { g.Key.UserId, g.Key.LoungeShowId, DistinctActions = g.Select(x => x.Action).Distinct().Count() })
            .ToListAsync(ct);

        foreach (var r in rows)
            Get(scores, r.UserId, r.LoungeShowId).IntentCount = r.DistinctActions;
    }

    private async Task AccumulateWishlistAsync(
        Dictionary<(int, int), ScoreAccumulator> scores, CancellationToken ct)
    {
        var rows = await _ctx.Wishlists
            .Select(w => new { w.UserId, w.LoungeShowId })
            .ToListAsync(ct);

        foreach (var r in rows)
            Get(scores, r.UserId, r.LoungeShowId).Wishlisted = true;
    }

    private async Task AccumulateAttendanceAsync(
        Dictionary<(int, int), ScoreAccumulator> scores, CancellationToken ct)
    {
        var rows = await _ctx.Tickets
            .Where(t => t.BuyerId != null
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used))
            .Select(t => new { UserId = t.BuyerId!.Value, t.ShowId })
            .Distinct()
            .ToListAsync(ct);

        foreach (var r in rows)
            Get(scores, r.UserId, r.ShowId).Attended = true;
    }

    private async Task AccumulateDonationsAsync(
        Dictionary<(int, int), ScoreAccumulator> scores, CancellationToken ct)
    {
        var rows = await (
            from d in _ctx.Donations
            join p in _ctx.Performances on d.PerformanceId equals p.Id
            where d.DonorUserId != null && d.PaymentConfirmedAt != null
            select new { UserId = d.DonorUserId!.Value, p.LoungeShowId }
        ).Distinct().ToListAsync(ct);

        foreach (var r in rows)
            Get(scores, r.UserId, r.LoungeShowId).Donated = true;
    }

    private async Task AccumulateRatingsAsync(
        Dictionary<(int, int), ScoreAccumulator> scores, CancellationToken ct)
    {
        var rows = await _ctx.Ratings
            .Where(r => r.UserId != null && !r.IsRemoved)
            .Select(r => new { UserId = r.UserId!.Value, r.LoungeShowId, r.Score })
            .ToListAsync(ct);

        foreach (var r in rows)
            Get(scores, r.UserId, r.LoungeShowId).RatingStars = r.Score;
    }

    private static ScoreAccumulator Get(Dictionary<(int, int), ScoreAccumulator> scores, int userId, int showId)
    {
        var key = (userId, showId);
        if (!scores.TryGetValue(key, out var acc))
        {
            acc = new ScoreAccumulator();
            scores[key] = acc;
        }
        return acc;
    }

    private sealed class ScoreAccumulator
    {
        public int ViewCount;
        public int IntentCount;
        public bool Wishlisted;
        public bool Attended;
        public bool Donated;
        public int RatingStars;

        public float TotalScore =>
            Math.Min(ViewCount, MaxViewCount) * ViewWeightPerAction +
            Math.Min(IntentCount, MaxIntentCount) * IntentWeightPerAction +
            (Wishlisted ? WishlistWeight : 0f) +
            (Attended ? AttendedWeight : 0f) +
            (Donated ? DonatedWeight : 0f) +
            (RatingStars > 0 ? RatingStars / 5f * MaxRatingWeight : 0f);
    }
}
