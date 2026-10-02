using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

// MLACP-513: ViewingSessionId = phiên xem trình duyệt này đã nhận lần trước (tuỳ chọn) — gửi lại để tải lại trang không
// bị tính thành một thiết bị mới.
public sealed record GetLivestreamDetailQuery(Guid LivestreamId, string? ViewingSessionId = null) : IQuery<LivestreamDetailDto>;
