using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// W27 — releases Settlement rows whose ScheduledAt has passed: writes the payout ledger
/// journal and marks the row Released. D16: a Final30 tranche is only auto-released if the
/// show's actual/scheduled duration ratio meets the configured completion threshold —
/// otherwise it's parked as PendingReview for Admin to decide.
/// </summary>
public sealed class SettlementReleaseJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILedgerService _ledger;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;
    private readonly ILogger<SettlementReleaseJob> _logger;

    public SettlementReleaseJob(
        ApplicationDbContext ctx, ILedgerService ledger, ISystemConfigService config,
        INotificationService notifications, ILogger<SettlementReleaseJob> logger)
    {
        _ctx = ctx;
        _ledger = ledger;
        _config = config;
        _notifications = notifications;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Filter by Status server-side, then filter ScheduledAt client-side — combining an
        // enum equality with a DateTimeOffset comparison in one query fails to translate
        // under the SQLite provider used in tests.
        var scheduled = await _ctx.Settlements
            .Where(s => s.Status == SettlementStatus.Scheduled)
            .ToListAsync(ct);
        var due = scheduled.Where(s => s.ScheduledAt <= now).ToList();

        if (due.Count == 0) return;

        var threshold = await _config.GetDecimalAsync(
            ConfigKeys.SettlementCompletionThresholdPct, 0.70m, ct);

        // A payment whose refund is still awaiting an Admin decision must not be paid out to the
        // owner yet. ProcessRefundRequest is the ONLY thing that shrinks a scheduled settlement to
        // match a refund, and it is a manual Admin action with no auto-processing job behind it —
        // while CancelLoungeShow (and ResolveComplaint / ResolveContentReport, which also cancel a
        // show) create the RefundRequest as Pending and leave the settlements untouched. So a show
        // cancelled with tickets sold left both tranches Scheduled: at showEnd+48h and showEnd+14d
        // this job paid the owner in full for tickets the buyers were about to be refunded 100% of,
        // and the platform covered both sides. The ledger is append-only, so undoing that needs a
        // manual reversing journal.
        //
        // Deferring rather than cancelling is what makes this safe in BOTH directions: if the
        // refund is approved, ProcessRefundRequest shrinks the tranche and the next daily run pays
        // the correct reduced amount; if it is rejected, nothing is pending any more and the next
        // run pays in full. Neither outcome is pre-judged here, and no money is stranded.
        var duePaymentIds = due.Select(s => s.PaymentId).Distinct().ToList();
        var paymentsAwaitingRefundDecision = (await _ctx.RefundRequests
                .Where(r => duePaymentIds.Contains(r.PaymentId) && r.Status == RefundRequestStatus.Pending)
                .Select(r => r.PaymentId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        // Nap mot lan, va chi khi thuc su co khoan bi giu — phan lon lan chay khong park cai nao.
        List<User>? admins = null;

        foreach (var settlement in due)
        {
            if (paymentsAwaitingRefundDecision.Contains(settlement.PaymentId))
            {
                _logger.LogWarning(
                    "Settlement release deferred — PaymentId={PaymentId} SettlementId={SettlementId} " +
                    "has a refund request still pending an Admin decision at {At}",
                    settlement.PaymentId, settlement.Id, now);
                continue;
            }

            // Same defer-don't-pre-judge rule, for a tranche with nowhere to pay into.
            // ScheduleSettlementHandler no longer refuses to create a settlement when the venue has
            // no default BankAccount — refusing there destroyed the buyer's already-paid purchase —
            // so it records the debt with a null destination instead. Writing the payout journal now
            // would credit the owner's ledger account for money no bank transfer can follow, and the
            // ledger is append-only. Hold it until an account exists; the next daily run pays it.
            if (settlement.BankAccountId is null)
            {
                _logger.LogError(
                    "Settlement release deferred — SettlementId={SettlementId} OwnerId={OwnerId} has no " +
                    "payout account. The venue must register a default BankAccount before this can be " +
                    "released at {At}",
                    settlement.Id, settlement.OwnerId, now);
                continue;
            }

            var show = await ShowForPaymentAsync(settlement.PaymentId, ct);
            var evidence = ShowCompletion.Evaluate(show);

            // MLACP-336. Hai chot, hai pham vi khac nhau — co y khong gop lam mot:
            //
            //   - "Chua tung bat dau" nghia la buoi dien KHONG dien ra. Khong co gi de tranh cai,
            //     nen ap cho CA HAI tranche. Tra nhanh 70% cho mot thu khong ton tai khong phai la
            //     "tra nhanh", no chi la tra sai som hon.
            //   - "Ti le duoi nguong" nghia la co dien ra nhung ngan hon du kien. Day la mot phan
            //     xet co the tranh cai (su co ky thuat, nghe si om, khan gia ve som), nen giu dung
            //     thiet ke D3: tra nhanh 70%, giu 30% lai cho ky ra soat.
            // MLACP-338: dung WasNeverDelivered chu khong phai IsDefinitelyUndelivered. Cai sau doi
            // buoi dien da duoc DONG lai (ActualEnd co gia tri), nen mot buoi dien ket o Published
            // vi job tu dong chua chay se roi vao "khong ket luan duoc" va van giai ngan binh thuong
            // — phong tra duoc tra tien cho mot buoi dien chua tung bat dau.
            //
            // Dieu do cung la thu khien cua hoan tien cua nguoi mua khong the mo an toan: neu tien
            // da ra khoi escrow thi hoan tien phai truy thu tu tai khoan chu phong tra. Hai chot
            // phai phu dung cung mot tap hop, neu khong thi mo mot ben lai lam hong ben kia.
            var undelivered = show is not null && ShowCompletion.WasNeverDelivered(show, now);
            var ratioFailed = settlement.ReleaseType == SettlementReleaseType.Final30
                              && !ShowCompletion.IsAcceptable(evidence, threshold);

            if (undelivered || ratioFailed)
            {
                settlement.Status = SettlementStatus.PendingReview;

                var reason = undelivered
                    ? "buoi dien chua tung duoc danh dau bat dau"
                    : $"ti le thoi luong buoi dien khong dat nguong {threshold}";

                // MLACP-335. Truoc day cho nay chi doi trang thai roi di tiep: khong log, khong
                // bao ai. Ma PendingReview lai khong co duong ra — job chi lay Scheduled nen
                // khong bao gio ngo lai. Tien cua phong tra nam do vinh vien trong khi
                // GetMyEarnings van dem no vao muc sap nhan duoc.
                _logger.LogWarning(
                    "Settlement parked for review — SettlementId={SettlementId} OwnerId={OwnerId} " +
                    "PaymentId={PaymentId} SoTien={Amount} LyDo={Reason} at {At}",
                    settlement.Id, settlement.OwnerId, settlement.PaymentId,
                    settlement.NetAmount, reason, now);

                admins ??= await _ctx.Users
                    .Where(u => u.Role == UserRole.Admin)
                    .ToListAsync(ct);

                var body = undelivered
                    ? $"Khoan {settlement.NetAmount:N0}d cua phong tra bi giu lai vi buoi dien chua " +
                      "tung duoc danh dau bat dau — co the no da khong dien ra. Can kiem chung: neu " +
                      "buoi dien that su khong dien ra thi nguoi mua ve can duoc hoan tien."
                    : $"Khoan {settlement.NetAmount:N0}d cua phong tra bi giu lai vi buoi dien " +
                      "khong chay du thoi luong da ban. Can kiem chung roi quyet chi tra hay giu lai.";

                foreach (var admin in admins)
                {
                    await _notifications.NotifyAsync(
                        admin.Id,
                        NotificationType.SettlementPendingReview,
                        "Khoan quyet toan can duyet",
                        body,
                        referenceType: "settlement",
                        referenceId: settlement.Id.ToString(),
                        ct: ct);
                }

                await _ctx.SaveChangesAsync(ct);
                continue;
            }

            var journalId = Guid.NewGuid().ToString("N");

            await _ledger.WriteJournalAsync(
                journalId,
                LedgerReferenceTypes.Settlement,
                settlement.Id.ToString(),
                settlement.PaymentId,
                new LedgerLine[]
                {
                    new(AccountType.Platform, null, settlement.NetAmount, IsDebit: true,
                        Description: $"Settlement #{settlement.Id} payout"),
                    new(AccountType.User, settlement.OwnerId, settlement.NetAmount, IsDebit: false,
                        Description: $"Settlement #{settlement.Id} payout")
                }, ct);

            settlement.Status = SettlementStatus.Released;
            settlement.ReleasedAt = now;
            settlement.LedgerJournalId = journalId;

            // Commit THIS settlement's own release before enqueuing its notification, not once at
            // the end of the whole batch — NotifyAsync's FCM push enqueues directly and durably to
            // Hangfire's own storage (IBackgroundJobService), independent of this DbContext's
            // transaction. With a single trailing SaveChangesAsync, a later settlement in the same
            // run throwing would leave this one's Release un-persisted while its push notification
            // had already fired; Hangfire's automatic retry would then re-process this
            // still-"Scheduled" settlement from scratch, sending a second "payment released"
            // notification for it. Saving per-item makes each settlement's release+notify atomic
            // and the loop safely resumable.
            await _ctx.SaveChangesAsync(ct);

            var (noticeTitle, noticeBody) = await ReleaseNoticeAsync(settlement, ct);
            await _notifications.NotifyAsync(
                settlement.OwnerId,
                NotificationType.SettlementReleased,
                noticeTitle,
                noticeBody,
                referenceType: "settlement",
                referenceId: settlement.Id.ToString(),
                ct: ct);

            // NotifyAsync only staged a Notification row (Add()) — flush that too before moving on,
            // so it isn't silently rolled into whatever the NEXT settlement's SaveChangesAsync
            // happens to touch (harmless today since Notification is its own row, but keeps this
            // settlement's unit of work fully self-contained).
            await _ctx.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// MLACP-361. Tien donate vua ve khong phai cua rieng phong tra: mot phan phai chuyen tiep cho nghe
    /// si. Thong bao phai noi dieu do va noi so tien — "khoan thanh toan da duoc giai ngan" chung chung
    /// thi chu phong tra khong biet minh con mot viec phai lam.
    /// </summary>
    private async Task<(string Title, string Body)> ReleaseNoticeAsync(Settlement settlement, CancellationToken ct)
    {
        var payment = await _ctx.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == settlement.PaymentId, ct);
        if (payment?.ReferenceType == DonationPayouts.PaymentReferenceType
            && int.TryParse(payment.ReferenceId, out var donationId)
            && await _ctx.Donations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == donationId, ct) is { } donation)
        {
            var rate = donation.PerformerShareRateSnapshot
                ?? await _config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
            var forPerformer = PaymentFeeCalculator.SplitDonationPayout(donation.Gross, donation.Net, rate).PerformerAmount;
            return ("Đã nhận tiền donate",
                $"Nền tảng đã chuyển {settlement.NetAmount:N0}đ tiền donate #{donation.Id} vào tài khoản của phòng trà. " +
                $"Hãy xác nhận đã nhận, rồi chuyển {forPerformer:N0}đ cho nghệ sĩ.");
        }

        return ("Đã nhận thanh toán",
            $"Khoản thanh toán {settlement.NetAmount:N0}đ ({settlement.ReleaseType}) đã được giải ngân.");
    }

    /// <summary>
    /// Buoi dien dung sau giao dich nay. Khong tim thay thi tra null — day la thieu du lieu that
    ///
    /// <para>Phep tinh nam o <see cref="ShowCompletion"/> chu khong phai o day: man hinh Admin phai
    /// hien dung con so da giu khoan nay lai, va mot quy tac co hai ban sao thi som muon cung lech
    /// (MLACP-335).</para>
    ///
    /// <para>su, khac han voi mot buoi dien da qua gio ma chua tung bat dau.</para>
    /// </summary>
    private async Task<LoungeShow?> ShowForPaymentAsync(int paymentId, CancellationToken ct)
    {
        var showId = await _ctx.Tickets
            .Where(t => t.PaymentId == paymentId)
            .Select(t => (int?)t.ShowId)
            .FirstOrDefaultAsync(ct);

        return showId is null
            ? null
            : await _ctx.LoungeShows.FirstOrDefaultAsync(s => s.Id == showId, ct);
    }
}
