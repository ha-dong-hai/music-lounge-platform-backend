using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Livestreams.Commands.ProcessMuxWebhook;

// Mux event -> hanh vi cua he thong (thong nhat sau khi can nhac danh doi):
//   video.live_stream.idle  : encoder da that su ngat va het reconnect window -> neu he thong dang
//     ghi nhan Live thi tu dong dong (mirror EndLivestreamCommandHandler ve mat trang thai), vi day
//     la truong hop encoder crash ma Owner quen bam "Ket thuc" - khong tu dong lam gi khac (khong
//     goi DeleteStreamAsync tren Mux, khong yeu cau VenueOperatorAccess vi day la actor he thong).
//   video.live_stream.active: CO CHU DICH khong tu dong chuyen Scheduled -> Live. Neu webhook nay la
//     nguon that duy nhat, Owner co the bat OBS thang toi Mux ma khong can goi qua
//     StartLivestreamCommand - bo qua kiem duyet noi dung cua Admin (W08) va yeu cau khai bao tac
//     quyen VCPMC (D19). Chi log de doi chieu/quan sat.
//   video.live_stream.disconnected, .warning: tin hieu "su co" tam thoi/canh bao - chi log, khong
//     doi trang thai (disconnected co the tu ket noi lai truoc khi Mux gui idle).
//   video.asset.ready (MLACP-121): Mux tu dong tao 1 Asset (ban ghi VOD) khi live stream duoc tao
//     voi new_asset_settings (MuxStreamService.CreateStreamAsync da bat san) va stream ket thuc.
//     Asset nay tra ve live_stream_id (lien ket nguoc ve Livestream.ProviderRef) va playback_ids[] -
//     dung playback_id public dau tien de dung URL HLS xem lai, luu vao RecordingUrl kem
//     ReplayAvailableUntil = now + livestream_replay_days (system_config).
//   Cac event khac (created/connected/recording/updated/enabled/disabled/deleted): bo qua.
//
// So nguoi xem realtime: Mux KHONG tra ve viewer count trong bat ky live-stream webhook nao (xac
// nhan qua tai lieu chinh thuc docs.mux.com/guides/listen-for-webhooks) - con so nay da co san qua
// SignalR LivestreamHub (dem ket noi that, gan voi quyen xem ve that), webhook nay khong dong gop
// gi them cho no.
internal sealed class ProcessMuxWebhookCommandHandler : IRequestHandler<ProcessMuxWebhookCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly IMuxWebhookVerifier _verifier;
    private readonly ISystemConfigService _config;
    private readonly ILogger<ProcessMuxWebhookCommandHandler> _logger;

    public ProcessMuxWebhookCommandHandler(
        IUnitOfWork uow,
        IMuxWebhookVerifier verifier,
        ISystemConfigService config,
        ILogger<ProcessMuxWebhookCommandHandler> logger)
    {
        _uow = uow;
        _verifier = verifier;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessMuxWebhookCommand request, CancellationToken ct)
    {
        if (!_verifier.VerifySignature(request.RawBody, request.SignatureHeader))
        {
            _logger.LogWarning("Mux webhook rejected — invalid or missing signature at {At}", DateTimeOffset.UtcNow);
            return false;
        }

        MuxWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<MuxWebhookEnvelope>(request.RawBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Mux webhook rejected — malformed JSON body at {At}", DateTimeOffset.UtcNow);
            return true; // signature was valid — acknowledge so Mux stops retrying an unparsable body
        }

        if (envelope?.Data is null || string.IsNullOrEmpty(envelope.Type))
        {
            _logger.LogInformation("Mux webhook ignored — missing type/data at {At}", DateTimeOffset.UtcNow);
            return true;
        }

        if (envelope.Type == "video.asset.ready")
            return await HandleAssetReadyAsync(envelope.Data, ct);

        // Voi moi event live_stream.* con lai, data.id chinh la live stream id (khac voi
        // video.asset.ready o tren, noi data.id la asset id — lien ket ve live stream qua
        // data.live_stream_id thay vi data.id).
        var providerRef = envelope.Data.Id;
        if (string.IsNullOrEmpty(providerRef))
        {
            _logger.LogInformation("Mux webhook ignored — missing data.id at {At}", DateTimeOffset.UtcNow);
            return true;
        }

        if (envelope.Type is "video.live_stream.disconnected" or "video.live_stream.warning")
        {
            _logger.LogWarning(
                "Mux webhook signal: Type={Type} ProviderRef={ProviderRef} at {At}",
                envelope.Type, providerRef, DateTimeOffset.UtcNow);
            return true;
        }

        if (envelope.Type != "video.live_stream.idle")
            return true; // bao gom ca video.live_stream.active — co chu dich khong tu chuyen Scheduled->Live

        return await HandleLiveStreamIdleAsync(providerRef, ct);
    }

    private async Task<bool> HandleLiveStreamIdleAsync(string providerRef, CancellationToken ct)
    {
        var livestreams = await _uow.Repository<Livestream, int>()
            .FindAsync(l => l.ProviderRef == providerRef, ct);
        var livestream = livestreams.FirstOrDefault();
        if (livestream is null)
        {
            _logger.LogInformation(
                "Mux webhook ignored — no Livestream found for ProviderRef={ProviderRef} at {At}",
                providerRef, DateTimeOffset.UtcNow);
            return true;
        }

        if (livestream.Status != LivestreamStatus.Live)
        {
            _logger.LogInformation(
                "Mux idle webhook no-op — LivestreamId={LivestreamId} already {Status} at {At}",
                livestream.Id, livestream.Status, DateTimeOffset.UtcNow);
            return true;
        }

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct);
        if (show is null)
        {
            _logger.LogWarning(
                "Mux idle webhook — LoungeShow {ShowId} not found for LivestreamId={LivestreamId} at {At}",
                livestream.LoungeShowId, livestream.Id, DateTimeOffset.UtcNow);
            return true;
        }

        var now = DateTimeOffset.UtcNow;

        livestream.Status = LivestreamStatus.Ended;
        livestream.EndedAt = now;
        livestream.ViewerCount = 0;
        _uow.Repository<Livestream, int>().Update(livestream);

        var ratingWindowDays = await _config.GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);
        show.Status = LoungeShowStatus.Ended;
        show.ActualEnd = now;
        show.RatingOpenUntil = now.AddDays(ratingWindowDays);
        _uow.Repository<LoungeShow, int>().Update(show);

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Mux idle webhook auto-ended livestream — LivestreamId={LivestreamId} ShowId={ShowId} " +
            "(encoder disconnected without an explicit End call) at {At}",
            livestream.Id, show.Id, now);

        return true;
    }

    private async Task<bool> HandleAssetReadyAsync(MuxWebhookData data, CancellationToken ct)
    {
        var liveStreamId = data.LiveStreamId;
        if (string.IsNullOrEmpty(liveStreamId))
        {
            // Asset khong duoc tao tu live stream (vd upload truc tiep) — khong lien quan gi den
            // he thong nay, moi Asset cua chung ta deu phai xuat phat tu 1 Livestream.
            _logger.LogInformation(
                "Mux asset.ready ignored — no live_stream_id (not created from a livestream) at {At}",
                DateTimeOffset.UtcNow);
            return true;
        }

        var playbackId = data.PlaybackIds?.FirstOrDefault(p => p.Policy == "public")?.Id;
        if (playbackId is null)
        {
            _logger.LogWarning(
                "Mux asset.ready — no public playback_id for LiveStreamProviderRef={ProviderRef} at {At}",
                liveStreamId, DateTimeOffset.UtcNow);
            return true;
        }

        var livestreams = await _uow.Repository<Livestream, int>()
            .FindAsync(l => l.ProviderRef == liveStreamId, ct);
        var livestream = livestreams.FirstOrDefault();
        if (livestream is null)
        {
            _logger.LogInformation(
                "Mux asset.ready ignored — no Livestream found for ProviderRef={ProviderRef} at {At}",
                liveStreamId, DateTimeOffset.UtcNow);
            return true;
        }

        var replayDays = await _config.GetIntAsync(ConfigKeys.LivestreamReplayDays, 30, ct);
        var now = DateTimeOffset.UtcNow;

        livestream.RecordingUrl = $"https://stream.mux.com/{playbackId}.m3u8";
        livestream.ReplayAvailableUntil = now.AddDays(replayDays);
        _uow.Repository<Livestream, int>().Update(livestream);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Mux asset.ready — recording saved for LivestreamId={LivestreamId} ReplayAvailableUntil={ReplayAvailableUntil} at {At}",
            livestream.Id, livestream.ReplayAvailableUntil, now);

        return true;
    }

    private sealed record MuxWebhookEnvelope(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] MuxWebhookData? Data);

    private sealed record MuxWebhookData(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("live_stream_id")] string? LiveStreamId,
        [property: JsonPropertyName("playback_ids")] MuxPlaybackId[]? PlaybackIds);

    private sealed record MuxPlaybackId(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("policy")] string Policy);
}
