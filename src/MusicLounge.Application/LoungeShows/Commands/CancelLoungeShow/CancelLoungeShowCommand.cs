using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

/// <param name="Reason">MLACP-676: loại lý do (<c>ShowCancellationReason</c>). Bắt buộc khi chủ phòng trà huỷ buổi ĐÃ MỞ
/// BÁN; Admin huỷ thì không cần (Admin không tự xét chính mình).</param>
/// <param name="Detail">Mô tả cụ thể, tối thiểu <c>ShowCancellationReasons.MinDetailLength</c> ký tự.</param>
/// <param name="EvidenceUrl">Ảnh/tệp bằng chứng tuỳ chọn (đã tải lên trước).</param>
public sealed record CancelLoungeShowCommand(
    Guid ShowId, string? Reason = null, string? Detail = null, string? EvidenceUrl = null) : ICommand;
