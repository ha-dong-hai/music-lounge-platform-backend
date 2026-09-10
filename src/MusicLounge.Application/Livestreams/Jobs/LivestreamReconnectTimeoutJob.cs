using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Livestreams.Jobs;

// MLACP-191: kich hoat tu ProcessMuxWebhookCommandHandler ngay khi nhan video.live_stream.disconnected
// (Status -> Reconnecting), len lich chay sau livestream_reconnect_timeout_minutes (mac dinh 5 phut).
// Neu den luc chay ma livestream da tu ket noi lai (Status != Reconnecting) hoac da co 1 chu ky ngat/
// ket noi lai KHAC xay ra sau do (DisconnectedAt khac gia tri da ghi nhan luc enqueue) thi bo qua —
// job nay khong phai nguon that duy nhat, chi la "het gio cho" cho dung 1 lan ngat cu the.
public sealed class LivestreamReconnectTimeoutJob
{
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;
    private readonly ILivestreamHubService _hubService;
    private readonly INotificationService _notifications;
    private readonly ILogger<LivestreamReconnectTimeoutJob> _logger;

    public LivestreamReconnectTimeoutJob(
        IUnitOfWork uow,
        ISystemConfigService config,
        ILivestreamHubService hubService,
        INotificationService notifications,
        ILogger<LivestreamReconnectTimeoutJob> logger)
    {
        _uow = uow;
        _config = config;
        _hubService = hubService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task ExecuteAsync(int livestreamId, DateTimeOffset disconnectedAt)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(livestreamId);
        if (livestream is null) return;

        if (livestream.Status != LivestreamStatus.Reconnecting || livestream.DisconnectedAt != disconnectedAt)
        {
            _logger.LogInformation(
                "Reconnect-timeout job no-op — LivestreamId={LivestreamId} Status={Status} at {At}",
                livestreamId, livestream.Status, DateTimeOffset.UtcNow);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        livestream.Status = LivestreamStatus.Failed;
        livestream.EndedAt = now;
        livestream.ViewerCount = 0;
        _uow.Repository<Livestream, int>().Update(livestream);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId);
        if (show is not null)
        {
            // This is the most likely of the four paths to hit a cancelled show: CancelLoungeShow
            // blocks only on livestream Status==Live, so an Owner can cancel during the reconnect
            // window and this job then fires minutes later against a show that is already
            // Cancelled and fully refunded.
            //
            // MLACP-353: voi show Hybrid, mat stream khong con dong ca buoi — phong that van dien.
            var outcome = await StreamLoss.ApplyToShowAsync(
                _uow, _config, _notifications, show, "mất kết nối quá thời gian chờ", now, CancellationToken.None);

            if (outcome == StreamLossOutcome.AlreadyTerminal)
                _logger.LogWarning(
                    "Reconnect-timeout left ShowId={ShowId} at {ShowStatus} — LivestreamId={LivestreamId} " +
                    "marked Failed but the show had already reached a terminal state at {At}",
                    show.Id, show.Status, livestream.Id, now);
            else if (outcome == StreamLossOutcome.ShowKeptOpenForRoom)
                _logger.LogInformation(
                    "Reconnect-timeout on Hybrid ShowId={ShowId} — livestream Failed, show kept Ongoing for " +
                    "the room at {At}", show.Id, now);
        }

        await _uow.SaveChangesAsync();

        _logger.LogWarning(
            "Livestream marked Failed — reconnect timeout elapsed without reconnection. " +
            "LivestreamId={LivestreamId} ShowId={ShowId} DisconnectedAt={DisconnectedAt} at {At}",
            livestreamId, livestream.LoungeShowId, disconnectedAt, now);

        await _hubService.BroadcastLivestreamFailedAsync(livestreamId);
    }
}
