using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Livestream : Common.AuditableEntity<Guid>
{
    public Guid LoungeShowId { get; set; }
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
    // MLACP-510: KHONG CON doc/ghi (he thong khong co xem lai). Hai cot con trong DB vi expand–contract: deploy code
    // nay truoc, roi MLACP-511 moi xoa cot + xoa hai thuoc tinh nay (xoa cot truoc khi deploy se lam code cu dang chay
    // vo khi EF doc cot da mat).
    public string? RecordingUrl { get; set; }
    public DateTimeOffset? ReplayAvailableUntil { get; set; }
    public Guid? TerminatedById { get; set; }
    public string? TerminatedReason { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public User? TerminatedBy { get; set; }
    public ICollection<LivestreamChatMessage> ChatMessages { get; set; } = [];
}
