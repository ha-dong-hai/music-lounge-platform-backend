using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Livestream : Common.AuditableEntity<int>
{
    public int LoungeShowId { get; set; }
    public string? Provider { get; set; }
    public string? ProviderRef { get; set; }
    public string? RtmpUrl { get; set; }
    public string? StreamKey { get; set; }
    public string? HlsUrl { get; set; }
    public LivestreamStatus Status { get; set; } = LivestreamStatus.Scheduled;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    // MLACP-191: moc thoi gian nhan tin hieu ngat ket noi gan nhat (video.live_stream.disconnected) —
    // null khi khong o trang thai Reconnecting. Dung de doi chieu voi job timeout 5 phut (guard chong
    // job cu bi tre khi da co 1 chu ky ngat/ket noi lai khac xay ra sau do).
    public DateTimeOffset? DisconnectedAt { get; set; }
    public bool IsFree { get; set; } = true;
    public bool ChatEnabled { get; set; } = true;
    public int ViewerCount { get; set; }
    public int PeakViewerCount { get; set; }
    public int TotalViews { get; set; }
    public string? RecordingUrl { get; set; }
    // MLACP-121: set cung luc voi RecordingUrl khi Mux bao asset (ban ghi) da san sang
    // (video.asset.ready) - null nghia la chua co ban ghi hoac khong gioi han thoi gian xem lai.
    public DateTimeOffset? ReplayAvailableUntil { get; set; }
    public int? TerminatedById { get; set; }
    public string? TerminatedReason { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public User? TerminatedBy { get; set; }
    public ICollection<LivestreamChatMessage> ChatMessages { get; set; } = [];
}
