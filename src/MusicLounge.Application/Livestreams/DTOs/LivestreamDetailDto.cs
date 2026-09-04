using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Livestreams.DTOs;

public sealed record LivestreamDetailDto(
    int Id,
    int LoungeShowId,
    string ShowName,
    LivestreamStatus Status,
    string? HlsUrl,
    int ViewerCount,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string? TerminatedReason,
    bool UserHasAccess,
    string? RecordingUrl,
    // Chỉ có giá trị khi caller là khán giả có vé PPV thật (isGenuineTicketHolder) — client dùng
    // để gọi POST {id}/heartbeat định kỳ giữ phiên sống. Null với Admin/venue-operator/livestream
    // miễn phí (những nhánh không bị giới hạn số phiên đồng thời).
    string? ViewingSessionId);
