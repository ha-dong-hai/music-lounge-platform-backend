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
    private readonly ILogger<LivestreamReconnectTimeoutJob> _logger;

    public LivestreamReconnectTimeoutJob(
        IUnitOfWork uow,
        ISystemConfigService config,
        ILivestreamHubService hubService,
        ILogger<LivestreamReconnectTimeoutJob> logger)
    {
        _uow = uow;
        _config = config;
        _hubService = hubService;
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
            var ratingWindowDays = await _config.GetIntAsync(ConfigKeys.RatingWindowDays, 7);
            show.Status = LoungeShowStatus.Ended;
            show.ActualEnd = now;
            show.RatingOpenUntil = now.AddDays(ratingWindowDays);
            _uow.Repository<LoungeShow, int>().Update(show);
        }

        await _uow.SaveChangesAsync();

        _logger.LogWarning(
            "Livestream marked Failed — reconnect timeout elapsed without reconnection. " +
            "LivestreamId={LivestreamId} ShowId={ShowId} DisconnectedAt={DisconnectedAt} at {At}",
            livestreamId, livestream.LoungeShowId, disconnectedAt, now);

        await _hubService.BroadcastLivestreamFailedAsync(livestreamId);
    }
}
