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
    string? ViewingSessionId,
    // MLACP-467-class gap: CreateLivestreamCommand nhận IsFree/ChatEnabled để ghi nhưng trước đây
    // không DTO đọc nào trả lại — màn bật/tắt chat (UC-71) phải set mù vì không biết trạng thái hiện
    // tại, và người vận hành không đọc được luồng đang miễn phí hay bán vé PPV.
    bool IsFree = true,
    bool ChatEnabled = true);
