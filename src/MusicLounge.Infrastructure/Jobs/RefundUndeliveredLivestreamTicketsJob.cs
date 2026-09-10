using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-340 — hoàn 100% tự động cho vé livestream của buổi diễn chưa từng lên sóng.
///
/// <para><b>Vì sao trường hợp này được xử lý tự động, khác với vé offline.</b> Với vé livestream,
/// nền tảng <b>chính là kênh giao hàng</b>. Nếu <c>Livestream.Status</c> chưa bao giờ là
/// <c>Live</c> thì không một người mua nào xem được gì — và đó là bằng chứng nằm trong hồ sơ của
/// chính nền tảng, không phải lời cáo buộc nhắm vào phòng trà, không phải chuyện phải đi hỏi ai.
/// Ticketmaster cũng chỉ hoàn tự động cho sự kiện đã xác định là bị huỷ; những gì còn tranh cãi thì
/// đi đường khác.</para>
///
/// <para><b>Cặp giá trị này không mơ hồ.</b> <c>StartLivestream</c> đặt <c>Livestream.Status = Live</c>
/// và <c>show.ActualStart</c> cùng một lúc. Đường webhook Mux chỉ chuyển <c>Reconnecting → Live</c>,
/// tức phải từng <c>Live</c> trước đó — lần lên sóng đầu tiên chỉ có đúng một đường. Và với show
/// <c>Hybrid</c>, <c>StartLoungeShow</c> từ chối thẳng nếu show có livestream, nên một show như vậy
/// chỉ có <b>đúng một nút bắt đầu</b>.</para>
///
/// <para><b>Chỉ vé livestream.</b> Với show Hybrid, người mua vé Physical vẫn đi đường xác minh
/// riêng — phòng trà có thể đã chạy phòng thật. Bằng chứng về stream không nói được gì về phòng.</para>
///
/// <para><b>MLACP-347 — lên sóng rồi bị cắt ngang.</b> Trước đây stream đã lên sóng thì job này bỏ
/// qua hoàn toàn, và không đường nào khác nhặt lên: bốn đường kết thúc stream (chủ phòng trà bấm,
/// webhook Mux, Admin/kiểm duyệt gỡ, hết hạn chờ kết nối lại) đều chỉ đóng buổi diễn;
/// <c>CancelTicket</c> từ chối mọi vé của buổi diễn đã kết thúc; và vé đã xem là <c>Used</c> nên cả
/// nhánh trên lẫn <c>ResolveComplaint</c> đều không chạm tới. Người mua một buổi phát 40 phút trong
/// 2 tiếng đã bán chỉ còn cách tự đi khiếu nại.</para>
///
/// <para>Quy tắc dùng lại đúng ngưỡng <c>settlement_completion_threshold_pct</c> (D16) — ngưỡng mà
/// nền tảng đã dùng để coi một buổi diễn là "không đạt" khi quyết toán. Một con số, một định nghĩa
/// cho cả hai phía. Dưới ngưỡng thì hoàn 100%, khớp với mọi đường hoàn do nền tảng ép khác (huỷ
/// show, gỡ nội dung, khiếu nại, chưa từng lên sóng). Đạt ngưỡng mà vẫn bị cắt bất thường thì không
/// hoàn tự động — chỉ báo cho người mua biết chuyện gì đã xảy ra, kèm lối khiếu nại.</para>
/// </summary>
public sealed class RefundUndeliveredLivestreamTicketsJob
{
    private const int DefaultGraceHours = 6;

    /// <summary>
    /// Chỉ xét stream kết thúc trong khoảng này. Tranche cuối được giải ngân 14 ngày sau buổi diễn —
    /// quá mốc đó thì nhánh tự động không còn gì để giữ lại, còn người mua vẫn có đường khiếu nại.
    /// </summary>
    private const int CutShortLookbackDays = 14;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;
    private readonly ILogger<RefundUndeliveredLivestreamTicketsJob> _logger;

    public RefundUndeliveredLivestreamTicketsJob(
        ApplicationDbContext ctx,
        ISystemConfigService config,
        INotificationService notifications,
        ILogger<RefundUndeliveredLivestreamTicketsJob> logger)
    {
        _ctx = ctx;
        _config = config;
        _notifications = notifications;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Hai nhánh độc lập. Tách thành hai hàm vì nhánh đầu return sớm khi không có gì để làm —
        // viết chung một thân hàm thì nhánh sau sẽ lặng lẽ không bao giờ chạy.
        await RefundNeverAiredAsync(now, ct);
        await HandleCutShortAsync(now, ct);
    }

    // ── Chưa từng lên sóng (MLACP-340) ──────────────────────────────────────

    private async Task RefundNeverAiredAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Cùng biên an toàn AutoEndStaleShowsJob dùng: đợi quá hạn rồi mới kết luận, để không hoàn
        // nhầm một buổi diễn bắt đầu rất trễ. StartLivestream không chặn theo đồng hồ.
        var graceHours = await _config.GetIntAsync(ConfigKeys.ShowAutoEndGraceHours, DefaultGraceHours, ct);

        // Lọc trạng thái phía server, so thời gian phía client — cùng giới hạn provider SQLite đã
        // ghi khắp thư mục này. Cancelled bỏ qua: đường đó đã có cơ chế hoàn riêng ở
        // CancelLoungeShow, và tickets ở đó đã Cancelled sẵn.
        var candidates = await _ctx.LoungeShows
            .Where(s => s.Status != LoungeShowStatus.Cancelled)
            .ToListAsync(ct);

        var undelivered = candidates
            .Where(s => ShowCompletion.WasNeverDelivered(s, now.AddHours(-graceHours)))
            .Select(s => s.Id)
            .ToList();

        if (undelivered.Count == 0) return;

        // Chỉ những buổi diễn thật sự có livestream — với show thuần offline thì không có bằng
        // chứng nào ở đây cả.
        var showIdsWithLivestream = (await _ctx.Livestreams
                .Where(l => undelivered.Contains(l.LoungeShowId))
                .Select(l => l.LoungeShowId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        if (showIdsWithLivestream.Count == 0) return;

        foreach (var showId in showIdsWithLivestream)
            await RefundLivestreamTicketsAsync(showId, now, ct);
    }

    private async Task RefundLivestreamTicketsAsync(int showId, DateTimeOffset now, CancellationToken ct)
    {
        var show = await _ctx.LoungeShows.FirstOrDefaultAsync(s => s.Id == showId, ct);
        if (show is null) return;

        // Chỉ vé đã xác nhận: vé người mua tự đòi lại rồi (MLACP-338) đã sang Cancelled, nên chốt
        // này cũng là chốt chống tạo yêu cầu hoàn trùng.
        var tickets = await _ctx.Tickets
            .Where(t => t.ShowId == showId && t.Status == TicketStatus.Confirmed)
            .ToListAsync(ct);

        var affected = await KeepLivestreamTierAsync(tickets, ct);
        if (affected.Count == 0) return;

        var priceById = await PriceByIdAsync(affected, ct);

        var created = 0;

        foreach (var ticket in affected)
        {
            ticket.Status = TicketStatus.Cancelled;

            if (ticket.PaymentId is null) continue;

            _ctx.RefundRequests.Add(new RefundRequest
            {
                PaymentId = ticket.PaymentId.Value,
                RequestedBy = ticket.BuyerId,
                Reason = "Buổi diễn không được phát sóng — hoàn 100%",
                AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                RefundPercentage = 100m,
                Status = RefundRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            created++;

            if (ticket.BuyerId is int buyerId)
                await _notifications.NotifyAsync(
                    buyerId,
                    NotificationType.EventCancelled,
                    "Buổi diễn không được phát sóng",
                    $"\"{show.Name}\" chưa bao giờ lên sóng nên bạn không xem được gì. Vé của bạn đã " +
                    "được huỷ và chúng tôi đã tự động tạo yêu cầu hoàn 100% tiền vé — bạn không cần " +
                    "làm gì thêm.",
                    referenceType: "show",
                    referenceId: show.Id.ToString(),
                    ct: ct);
        }

        _logger.LogWarning(
            "Hoan tu dong ve livestream cua buoi dien chua tung len song — ShowId={ShowId} " +
            "SoVe={TicketCount} SoYeuCauHoan={RefundCount} at {At}",
            show.Id, affected.Count, created, now);

        await _ctx.SaveChangesAsync(ct);
    }

    // ── Lên sóng rồi bị cắt ngang (MLACP-347) ───────────────────────────────

    private async Task HandleCutShortAsync(DateTimeOffset now, CancellationToken ct)
    {
        var threshold = await _config.GetDecimalAsync(
            ConfigKeys.SettlementCompletionThresholdPct, 0.70m, ct);

        // Lọc trạng thái phía server, so thời gian phía client — cùng giới hạn provider SQLite.
        var finished = await _ctx.Livestreams
            .Where(l => l.Status == LivestreamStatus.Ended
                        || l.Status == LivestreamStatus.Terminated
                        || l.Status == LivestreamStatus.Failed)
            .ToListAsync(ct);

        var since = now.AddDays(-CutShortLookbackDays);
        var recent = finished
            .Where(l => l.StartedAt is not null && l.EndedAt is { } endedAt && endedAt >= since)
            .ToList();

        foreach (var livestream in recent)
        {
            var show = await _ctx.LoungeShows.FirstOrDefaultAsync(s => s.Id == livestream.LoungeShowId, ct);

            // Show bị huỷ trong lúc stream đang chờ kết nối lại đã được CancelLoungeShow hoàn 100%.
            if (show is null || show.Status == LoungeShowStatus.Cancelled) continue;

            var evidence = ShowCompletion.EvaluateLivestream(show, livestream);

            if (evidence.Verdict == ShowCompletionVerdict.Measured
                && !ShowCompletion.IsAcceptable(evidence, threshold))
            {
                await RefundCutShortAsync(show, evidence.Ratio!.Value, threshold, now, ct);
            }
            else if (livestream.Status is LivestreamStatus.Failed or LivestreamStatus.Terminated)
            {
                // Chủ phòng trà tự bấm kết thúc khi đã đạt ngưỡng là một buổi diễn bình thường —
                // không có gì phải báo. Còn stream mất tín hiệu hoặc bị gỡ thì người xem cần biết
                // chuyện gì đã xảy ra, dù không được hoàn tự động.
                await NotifyInterruptedAsync(show, livestream, evidence, threshold, ct);
            }
        }
    }

    private async Task RefundCutShortAsync(
        LoungeShow show, decimal ratio, decimal threshold, DateTimeOffset now, CancellationToken ct)
    {
        // Confirmed: mua mà chưa vào xem. Used: đã xem một phần. Cả hai đều trả tiền cho thứ không
        // được giao đủ. Vé đã chuyển trạng thái ở lần chạy trước không còn nằm trong tập này — đó
        // là chốt chống trùng, và nó theo từng VÉ: một thanh toán có thể gồm nhiều vé, nên chống
        // trùng theo thanh toán sẽ bỏ sót vé còn lại của người đã huỷ một vé từ trước.
        var tickets = await _ctx.Tickets
            .Where(t => t.ShowId == show.Id
                        && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used))
            .ToListAsync(ct);

        // Vé không có thanh toán thì không có gì để hoàn, và không có lý do gì để đổi trạng thái.
        var affected = (await KeepLivestreamTierAsync(tickets, ct))
            .Where(t => t.PaymentId is not null)
            .ToList();
        if (affected.Count == 0) return;

        var priceById = await PriceByIdAsync(affected, ct);
        var deliveredPct = WholePercent(ratio);
        var thresholdPct = WholePercent(threshold);

        foreach (var ticket in affected)
        {
            // Used -> Refunded chứ không phải Cancelled: người đã xem vẫn giữ quyền đánh giá.
            ticket.Status = ticket.Status == TicketStatus.Used
                ? TicketStatus.Refunded
                : TicketStatus.Cancelled;

            _ctx.RefundRequests.Add(new RefundRequest
            {
                PaymentId = ticket.PaymentId!.Value,
                RequestedBy = ticket.BuyerId,
                Reason = $"Buổi phát sóng chỉ chạy {deliveredPct}% thời lượng đã bán " +
                         $"(ngưỡng {thresholdPct}%) — hoàn 100%",
                AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                RefundPercentage = 100m,
                Status = RefundRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        var buyerIds = affected
            .Where(t => t.BuyerId is not null)
            .Select(t => t.BuyerId!.Value)
            .Distinct();

        foreach (var buyerId in buyerIds)
        {
            await _notifications.NotifyAsync(
                buyerId,
                NotificationType.LivestreamCutShort,
                "Buổi phát sóng bị cắt ngang",
                $"\"{show.Name}\" chỉ phát được {deliveredPct}% thời lượng đã bán. Chúng tôi đã tự " +
                "động tạo yêu cầu hoàn 100% tiền vé livestream cho bạn — bạn không cần làm gì thêm. " +
                "Nếu bạn đã vào xem, bạn vẫn đánh giá được buổi diễn này.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }

        var ownerId = await _ctx.Lounges
            .Where(l => l.Id == show.LoungeId)
            .Select(l => (int?)l.OwnerId)
            .FirstOrDefaultAsync(ct);

        if (ownerId is int owner)
        {
            await _notifications.NotifyAsync(
                owner,
                NotificationType.LivestreamCutShort,
                "Buổi phát sóng không đủ thời lượng",
                $"\"{show.Name}\" chỉ phát được {deliveredPct}% thời lượng đã bán, dưới ngưỡng " +
                $"{thresholdPct}%. {affected.Count} vé livestream đã được tạo yêu cầu hoàn 100%; " +
                "các khoản này sẽ được trừ khỏi quyết toán của buổi diễn.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }

        _logger.LogWarning(
            "Hoan tu dong ve livestream cua buoi phat song bi cat ngang — ShowId={ShowId} " +
            "TiLeGiao={Ratio} Nguong={Threshold} SoVe={TicketCount} at {At}",
            show.Id, ratio, threshold, affected.Count, now);

        await _ctx.SaveChangesAsync(ct);
    }

    private async Task NotifyInterruptedAsync(
        LoungeShow show, Livestream livestream, ShowCompletionEvidence evidence, decimal threshold,
        CancellationToken ct)
    {
        var tickets = await _ctx.Tickets
            .Where(t => t.ShowId == show.Id
                        && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used))
            .ToListAsync(ct);

        var buyerIds = (await KeepLivestreamTierAsync(tickets, ct))
            .Where(t => t.BuyerId is not null)
            .Select(t => t.BuyerId!.Value)
            .Distinct()
            .ToList();
        if (buyerIds.Count == 0) return;

        var referenceId = show.Id.ToString();
        var alreadyTold = (await _ctx.Notifications
                .Where(n => buyerIds.Contains(n.UserId)
                            && n.Type == NotificationType.LivestreamCutShort
                            && n.ReferenceType == "show"
                            && n.ReferenceId == referenceId)
                .Select(n => n.UserId)
                .ToListAsync(ct))
            .ToHashSet();

        var what = livestream.Status == LivestreamStatus.Terminated
            ? "đã bị nền tảng dừng phát sóng"
            : "bị mất tín hiệu từ phòng trà và không kết nối lại được";

        // Không có giờ kết thúc khai báo thì không có con số nào đáng tin để nói.
        var measured = evidence.Verdict == ShowCompletionVerdict.Measured
            ? $" khi đã phát {WholePercent(evidence.Ratio!.Value)}% thời lượng đã bán (đạt ngưỡng " +
              $"{WholePercent(threshold)}%), nên vé không được hoàn tự động"
            : "";

        var told = 0;

        foreach (var buyerId in buyerIds.Where(id => !alreadyTold.Contains(id)))
        {
            await _notifications.NotifyAsync(
                buyerId,
                NotificationType.LivestreamCutShort,
                "Buổi phát sóng đã dừng giữa chừng",
                $"\"{show.Name}\" {what}{measured}. Nếu bạn thấy chưa thỏa đáng, hãy gửi khiếu nại " +
                "về buổi diễn này để Admin xem xét.",
                referenceType: "show",
                referenceId: referenceId,
                ct: ct);
            told++;
        }

        if (told == 0) return;

        _logger.LogInformation(
            "Bao nguoi mua buoi phat song bi dung giua chung (dat nguong, khong hoan tu dong) — " +
            "ShowId={ShowId} LivestreamStatus={Status} SoNguoi={Count}",
            show.Id, livestream.Status, told);

        await _ctx.SaveChangesAsync(ct);
    }

    // ── Dùng chung ──────────────────────────────────────────────────────────

    private async Task<List<Ticket>> KeepLivestreamTierAsync(List<Ticket> tickets, CancellationToken ct)
    {
        if (tickets.Count == 0) return tickets;

        var tierIds = tickets.Select(t => t.TierId).Distinct().ToList();
        var livestreamTierIds = (await _ctx.TicketTiers
                .Where(t => tierIds.Contains(t.Id) && t.AccessType == AccessType.Livestream)
                .Select(t => t.Id)
                .ToListAsync(ct))
            .ToHashSet();

        return tickets.Where(t => livestreamTierIds.Contains(t.TierId)).ToList();
    }

    private Task<Dictionary<int, decimal>> PriceByIdAsync(List<Ticket> tickets, CancellationToken ct)
    {
        var priceIds = tickets.Select(t => t.PriceId).Distinct().ToList();
        return _ctx.TicketPrices
            .Where(p => priceIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Price, ct);
    }

    /// <summary>
    /// Làm tròn xuống: 69,9% phải hiện là 69% — hiện "70%" cạnh "ngưỡng 70%" thì người đọc không
    /// hiểu vì sao bị hoàn.
    /// </summary>
    private static int WholePercent(decimal fraction) => (int)Math.Floor(fraction * 100m);
}
