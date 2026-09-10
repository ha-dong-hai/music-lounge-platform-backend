using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
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
/// </summary>
public sealed class RefundUndeliveredLivestreamTicketsJob
{
    private const int DefaultGraceHours = 6;

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
        if (tickets.Count == 0) return;

        var tierIds = tickets.Select(t => t.TierId).Distinct().ToList();
        var livestreamTierIds = (await _ctx.TicketTiers
                .Where(t => tierIds.Contains(t.Id) && t.AccessType == AccessType.Livestream)
                .Select(t => t.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var affected = tickets.Where(t => livestreamTierIds.Contains(t.TierId)).ToList();
        if (affected.Count == 0) return;

        var priceIds = affected.Select(t => t.PriceId).Distinct().ToList();
        var priceById = await _ctx.TicketPrices
            .Where(p => priceIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Price, ct);

        var created = 0;

        foreach (var ticket in affected)
        {
            ticket.Status = TicketStatus.Cancelled;

            if (ticket.PaymentId is null) continue;

            _ctx.RefundRequests.Add(new Domain.Entities.RefundRequest
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
}
