using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.LoungeShows;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// §6.8 — applies the delayed venue-status change and subscription compensation for
/// Suspension/Ban penalties once EffectiveAt arrives. Warning has no delay and is applied
/// immediately by IssuePenaltyCommandHandler, so it never appears here. Idempotent per penalty via
/// VenuePenalty.AppliedAt (set the moment this job actually processes a penalty) — NOT via
/// comparing against the venue's current Status, which was the original (buggy) approach: two
/// separate Suspension penalties on the same venue can both legitimately target LoungeStatus.Suspended,
/// and checking only the venue's status meant the second one's subscription compensation was
/// silently skipped as "already applied" when it never was.
/// </summary>
public sealed class ApplyDuePenaltiesJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _uow;
    private readonly IAsyncKeyedLock _lock;

    public ApplyDuePenaltiesJob(
        ApplicationDbContext ctx, INotificationService notifications, IUnitOfWork uow, IAsyncKeyedLock @lock)
    {
        _ctx = ctx;
        _notifications = notifications;
        _uow = uow;
        _lock = @lock;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Filter by Status server-side, then filter EffectiveAt client-side — same pattern as
        // SettlementReleaseJob/ReleaseExpiredHoldsJob: combining an enum-equality predicate with a
        // DateTimeOffset comparison in one query does not translate under the SQLite test provider.
        //
        // MLACP-369: moi an CON hieu luc, khong chi Active. Chu phong tra khang cao trong thoi gian bao truoc
        // (khang cao duoc trong 7 ngay tu luc ban hanh, tam khoa co hieu luc sau 24 gio) — Admin bac khang cao
        // thi an thanh Upheld, tuc van dung; truoc day job nay chi ap Active nen an do khong bao gio co hieu luc.
        var active = await _ctx.VenuePenalties
            .Where(p => PenaltyLifecycle.InForce.Contains(p.Status) && p.AppliedAt == null
                && (p.PenaltyType == PenaltyType.Suspension || p.PenaltyType == PenaltyType.Ban))
            .ToListAsync(ct);
        var due = active.Where(p => p.EffectiveAt <= now).ToList();
        if (due.Count == 0) return;

        foreach (var penalty in due)
        {
            var lounge = await _ctx.Lounges.FirstOrDefaultAsync(l => l.Id == penalty.LoungeId, ct);
            if (lounge is null) continue;

            // MLACP-373: truoc day job chi doi trang thai phong tra — ve da ban cho cac buoi sap toi van nguyen, nguoi
            // mua toi mot buoi dien khong con ai to chuc, khong ai duoc hoan. Huy TRUOC khi danh dau AppliedAt: job chet
            // giua chung thi lan chay sau lam tiep (buoi da huy khong con Published nen khong bi huy lai).
            var cancelled = await CancelShowsInsideAsync(penalty, now, ct);

            // MLACP-367: khong bao gio nhe di — mot lenh tam khoa co hieu luc sau lenh khoa vinh vien truoc
            // day ha Locked xuong Suspended, roi ExpireServedSuspensionsJob mo khoa luon khi het han.
            if (PenaltyLifecycle.StatusAfterImposing(lounge.Status, penalty.PenaltyType) is { } imposedStatus)
                lounge.Status = imposedStatus;
            penalty.AppliedAt = now;

            // MLACP-299: moc het han duoc chot tu day chu khong tinh lai o cho khac. Truoc day cot
            // nay khong ai ghi, va cung khong co gi go lenh treo ra — mot phong tra duoc bao "tam
            // khoa N ngay" thi bi khoa vinh vien. Tinh tu NOW chu khong tu EffectiveAt: N ngay bi
            // khoa phai la N ngay thuc su bi khoa, ma lenh chi bat dau co hieu luc tu luc job nay
            // chay. Ban vinh vien khong co moc het han.
            if (penalty.PenaltyType == PenaltyType.Suspension && penalty.SuspensionDays is int suspensionDays)
                penalty.SuspensionEnd = now.AddDays(suspensionDays);

            // Ordering by a DateTimeOffset column does not translate under the SQLite provider
            // used in tests (same limitation noted elsewhere in this codebase) — an owner should
            // only ever have one Active subscription at a time anyway, so fetch and pick client-side.
            var ownerSubscriptions = await _ctx.OwnerSubscriptions
                .Where(s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct);
            var subscription = ownerSubscriptions.OrderByDescending(s => s.ExpiresAt).FirstOrDefault();

            if (subscription is not null)
            {
                if (penalty.PenaltyType == PenaltyType.Suspension && penalty.SuspensionDays is int days)
                {
                    // §6.8 — compensate the Owner for the outage by pushing their subscription
                    // expiry back, so a suspension doesn't also cost them paid-for platform time.
                    subscription.ExpiresAt = subscription.ExpiresAt.AddDays(days);
                }
                else if (penalty.PenaltyType == PenaltyType.Ban)
                {
                    // MLACP-369: dung goi, khong hoan phi — xem PenaltySubscriptions.
                    PenaltySubscriptions.StopOnBan(subscription, now);
                }
            }

            // MLACP-260: commit THIS penalty's own AppliedAt+ledger-reversal before moving to the
            // next one, not once at the end of the whole batch — matches SettlementReleaseJob's
            // established pattern for the same reason. With a single trailing SaveChangesAsync, one
            // poison-pill penalty throwing mid-loop would roll back every EARLIER penalty in this run
            // too (AppliedAt never persisted for them), even though they were already fully
            // processed — not a double-write risk (AppliedAt==null re-query means they'd just be
            // reprocessed from scratch next run), but needlessly fragile and inconsistent with the
            // sibling job's own documented reasoning for doing this per-item.
            await _ctx.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.PenaltyIssued,
                penalty.PenaltyType == PenaltyType.Suspension ? "Phòng trà đã bị tạm khoá" : "Phòng trà đã bị khoá vĩnh viễn",
                $"\"{lounge.Name}\" hiện đã ở trạng thái {lounge.Status} theo phạt #{penalty.Id}." +
                (penalty.PenaltyType == PenaltyType.Ban && subscription is not null
                    ? " Gói dịch vụ đã dừng; phí gói không được hoàn khi phòng trà bị khoá vĩnh viễn do vi phạm. " +
                      "Nếu lệnh khoá được huỷ, gói được kích hoạt lại với đúng số ngày còn lại."
                    : "") +
                DescribeCancelled(penalty.PenaltyType, cancelled),
                referenceType: "venue_penalty",
                referenceId: penalty.Id.ToString(),
                ct: ct);

            await _ctx.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// MLACP-373 — các buổi diễn phòng trà không còn được tổ chức: khoá vĩnh viễn thì mọi buổi chưa diễn; tạm khoá thì
    /// các buổi bắt đầu trước khi hết hạn khoá (tính từ now — cùng mốc SuspensionEnd được chốt ở trên). Đi qua đúng
    /// đường huỷ của chủ phòng trà (<see cref="ShowCancellation"/>): hoàn 100% mọi vé, báo người giữ vé và người mua
    /// ban đầu.
    ///
    /// <para>Chỉ buổi Published chưa tới giờ bắt đầu: buổi đã qua giờ mà chưa bắt đầu thuộc đường "buổi diễn không
    /// được tổ chức" (MLACP-338); buổi đang diễn thì khán giả đã ở đó.</para>
    /// </summary>
    private async Task<ShowCancellation.Outcome> CancelShowsInsideAsync(
        VenuePenalty penalty, DateTimeOffset now, CancellationToken ct)
    {
        DateTimeOffset? until = penalty.PenaltyType switch
        {
            PenaltyType.Ban => DateTimeOffset.MaxValue,
            PenaltyType.Suspension when penalty.SuspensionDays is int days => now.AddDays(days),
            _ => null
        };
        var total = ShowCancellation.Outcome.None;
        if (until is not DateTimeOffset end) return total;

        // Loc trang thai phia server, so thoi gian phia client — cung ly do voi truy van an phat o tren.
        var showIds = (await _ctx.LoungeShows.AsNoTracking()
                .Where(s => s.LoungeId == penalty.LoungeId && s.Status == LoungeShowStatus.Published)
                .Select(s => new { s.Id, s.ScheduledStart })
                .ToListAsync(ct))
            .Where(s => s.ScheduledStart > now && s.ScheduledStart < end)
            .Select(s => s.Id)
            .ToList();

        foreach (var showId in showIds)
        {
            // Cung khoa voi CancelLoungeShow / ChangeLoungeShowFormat: chu phong tra huy cung luc thi khong thanh hai
            // lan hoan cho cung mot ve.
            await using var _ = await _lock.AcquireAsync($"show-status-change:{showId}", ct);
            var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(showId, ct);
            if (show is null || show.Status != LoungeShowStatus.Published) continue;

            total += await ShowCancellation.CancelAsync(
                _uow, _notifications, show, ShowCancellation.VenueStoppedTrading, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return total;
    }

    /// <summary>Câu báo chủ phòng trà — vé bán tại quầy không có tài khoản, chỉ phòng trà liên hệ được người mua.</summary>
    private static string DescribeCancelled(PenaltyType type, ShowCancellation.Outcome cancelled)
    {
        if (cancelled.Shows == 0) return "";
        var text = $" Đã huỷ {cancelled.Shows} buổi diễn " +
                   (type == PenaltyType.Ban ? "sắp tới" : "rơi vào thời gian tạm khoá") +
                   " và tự động tạo yêu cầu hoàn 100% cho người mua.";
        if (cancelled.WalkInTickets > 0)
            text += $" {cancelled.WalkInTickets} vé bán tại quầy không có tài khoản để nền tảng báo — phòng trà phải " +
                    "hoàn tiền mặt khi khách liên hệ và xác nhận đã trả trên hệ thống.";
        return text;
    }
}
