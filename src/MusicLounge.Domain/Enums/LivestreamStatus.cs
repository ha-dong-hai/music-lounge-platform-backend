namespace MusicLounge.Domain.Enums;

public enum LivestreamStatus
{
    Scheduled,
    Live,
    Ended,
    Terminated,
    // MLACP-191: encoder mat ket noi dot ngot (Mux video.live_stream.disconnected) trong khi dang
    // Live - he thong cho toi hinh dau vay (system_config: livestream_reconnect_timeout_minutes,
    // mac dinh 5 phut) de encoder tu ket noi lai, thay vi lap tuc coi la loi/ket thuc.
    Reconnecting,
    // Het thoi gian cho o tren ma khong nhan duoc tin hieu ket noi lai (video.live_stream.connected).
    Failed
}
