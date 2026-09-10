using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-341 — nói cho người ta biết khi một buổi diễn <b>offline</b> đã qua giờ mà chưa từng được
/// bấm Bắt đầu.
///
/// <para><b>Tiền đã an toàn rồi; thứ còn thiếu là giao tiếp.</b> Sau MLACP-336/338 cả hai tranche
/// quyết toán bị giữ lại và người mua tự đòi lại được 100%. Nhưng với vé Physical, họ phải
/// <b>tự nhận ra</b> — nền tảng biết từ giờ thứ sáu mà không nói gì. Đó đúng là chỗ làm hỏng uy tín:
/// người mua trả tiền cho nền tảng, nền tảng biết có vấn đề, và im lặng.</para>
///
/// <para><b>Vì sao không tự động hoàn như vé livestream.</b> Với vé offline nền tảng không phải kênh
/// giao hàng nên không kết luận được: buổi diễn có thể đã chạy thật mà phòng trà quên bấm Bắt đầu.
/// Tự động hoàn cho cả khán phòng vì một nút không được bấm sẽ là sai lầm nặng hơn. Nên hoàn
/// <b>theo từng người</b>: ai nói không diễn ra thì được 100%, ai đã dự thì không làm gì cả — và
/// phòng trà có diễn thật thì không mất gì.</para>
///
/// <para><b>Hai chặng, không phải một.</b> Báo chủ phòng trà trước: nếu buổi diễn có diễn ra, họ
/// liên hệ quản trị viên để được giải ngân, và người mua không bị làm phiền lần nào. Chỉ khi qua
/// cửa sổ đó mà chuyện vẫn chưa được giải quyết thì mới báo người mua.</para>
/// </summary>
public sealed class NotifyUndeliveredOfflineShowJob
{
    private const int DefaultGraceHours = 6;

    /// <summary>Mặc định khi <c>system_config</c> chưa có khoá.</summary>
    public const int DefaultConfirmationHours = 24;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;

    public NotifyUndeliveredOfflineShowJob(
        ApplicationDbContext ctx, ISystemConfigService config, INotificationService notifications)
    {
        _ctx = ctx;
        _config = config;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var graceHours = await _config.GetIntAsync(ConfigKeys.ShowAutoEndGraceHours, DefaultGraceHours, ct);
        var confirmationHours = await _config.GetIntAsync(
            ConfigKeys.ShowDeliveryConfirmationHours, DefaultConfirmationHours, ct);

        // Lọc trạng thái phía server, so thời gian phía client — cùng giới hạn provider SQLite đã
        // ghi khắp thư mục này. Cancelled bỏ qua: đường đó đã có cơ chế hoàn riêng.
        var candidates = await _ctx.LoungeShows
            .Where(s => s.Status != LoungeShowStatus.Cancelled)
            .ToListAsync(ct);

        var undelivered = candidates
            .Where(s => ShowCompletion.WasNeverDelivered(s, now.AddHours(-graceHours)))
            .ToList();

        if (undelivered.Count == 0) return;

        var notified = false;

        foreach (var show in undelivered)
        {
            var buyerIds = await PhysicalTicketBuyersAsync(show.Id, ct);
            if (buyerIds.Count == 0) continue;   // không có vé offline thì không có ai để nói

            notified |= await NotifyOwnerAsync(show, now, ct);

            // Chặng hai chỉ mở sau khi cửa sổ của phòng trà đã trôi qua, tính từ giờ kết thúc dự
            // kiến cộng biên an toàn — cùng mốc mà chặng một dùng.
            var buyersDueAt = ShowSchedule.EffectiveEnd(show)
                .AddHours(graceHours)
                .AddHours(confirmationHours);
            if (now < buyersDueAt) continue;

            foreach (var buyerId in buyerIds)
                notified |= await NotifyBuyerAsync(show, buyerId, ct);
        }

        // NotifyAsync mới chỉ Add vào change tracker — hợp đồng ghi rõ người gọi phải lưu.
        if (notified) await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Chỉ người mua vé <see cref="AccessType.Physical"/>. Vé livestream của cùng buổi diễn (show
    /// <c>Hybrid</c>) đã được hoàn tự động ở MLACP-340 vì ở đó bằng chứng kết luận được.
    /// </summary>
    private async Task<List<int>> PhysicalTicketBuyersAsync(int showId, CancellationToken ct)
    {
        var tickets = await _ctx.Tickets
            .Where(t => t.ShowId == showId && t.Status == TicketStatus.Confirmed && t.BuyerId != null)
            .Select(t => new { t.TierId, BuyerId = t.BuyerId!.Value })
            .ToListAsync(ct);
        if (tickets.Count == 0) return [];

        var tierIds = tickets.Select(t => t.TierId).Distinct().ToList();
        var physicalTierIds = (await _ctx.TicketTiers
                .Where(t => tierIds.Contains(t.Id) && t.AccessType == AccessType.Physical)
                .Select(t => t.Id)
                .ToListAsync(ct))
            .ToHashSet();

        return tickets
            .Where(t => physicalTierIds.Contains(t.TierId))
            .Select(t => t.BuyerId)
            .Distinct()
            .ToList();
    }

    private async Task<bool> NotifyOwnerAsync(LoungeShow show, DateTimeOffset now, CancellationToken ct)
    {
        var ownerId = await _ctx.Lounges
            .Where(l => l.Id == show.LoungeId)
            .Select(l => (int?)l.OwnerId)
            .FirstOrDefaultAsync(ct);
        if (ownerId is null) return false;

        if (await AlreadyToldAsync(ownerId.Value, show.Id, ct)) return false;

        await _notifications.NotifyAsync(
            ownerId.Value,
            NotificationType.ShowDeliveryUnconfirmed,
            "Chưa xác nhận được buổi diễn đã diễn ra",
            $"\"{show.Name}\" chưa từng được bấm Bắt đầu nên hệ thống không có gì để xác nhận buổi " +
            "diễn đã diễn ra. Khoản quyết toán đang được giữ lại. Nếu buổi diễn CÓ diễn ra, hãy " +
            "liên hệ quản trị viên để được đối chiếu và giải ngân. Nếu không, người mua vé sẽ được " +
            "hoàn 100% tiền vé.",
            referenceType: "show",
            referenceId: show.Id.ToString(),
            ct: ct);

        return true;
    }

    private async Task<bool> NotifyBuyerAsync(LoungeShow show, int buyerId, CancellationToken ct)
    {
        if (await AlreadyToldAsync(buyerId, show.Id, ct)) return false;

        // Diễn đạt là khoảng trống ghi nhận của nền tảng, không phải lời cáo buộc nhắm vào phòng
        // trà — vì đó đúng là sự thật, và vì buổi diễn có thể đã diễn ra bình thường.
        await _notifications.NotifyAsync(
            buyerId,
            NotificationType.ShowDeliveryUnconfirmed,
            "Chúng tôi chưa xác nhận được buổi diễn đã diễn ra",
            $"Hệ thống chưa ghi nhận được xác nhận rằng \"{show.Name}\" đã diễn ra. Nếu bạn đã tham " +
            "dự, bạn không cần làm gì cả. Nếu buổi diễn không diễn ra, bạn có thể yêu cầu hoàn 100% " +
            "tiền vé ngay trong phần vé của bạn.",
            referenceType: "show",
            referenceId: show.Id.ToString(),
            ct: ct);

        return true;
    }

    /// <summary>
    /// Chống trùng bằng chính dòng thông báo đã gửi, không thêm cột cờ mới — đúng cách
    /// <c>EventReminderJob</c> làm. Lọc theo người nhận nên chặng một và chặng hai không đụng nhau.
    /// </summary>
    private Task<bool> AlreadyToldAsync(int userId, int showId, CancellationToken ct)
        => _ctx.Notifications.AnyAsync(
            n => n.UserId == userId
                 && n.Type == NotificationType.ShowDeliveryUnconfirmed
                 && n.ReferenceType == "show"
                 && n.ReferenceId == showId.ToString(), ct);
}
