using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.DTOs;

public sealed record ComplaintDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    ComplaintCategory Category,
    string Description,
    string? EvidenceUrls,
    string? ContactPhone,
    ComplaintStatus Status,
    string? ComplainantName,
    string? AdminName,
    string? Resolution,
    ComplaintResolvedAction? ResolvedAction,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    // MLACP-617: hạn xử lý (cột SlaDeadline) — trước đây không trả ra, nên danh sách của Admin chỉ ghi được "Đã chờ 3 ngày"
    // mà không biết dòng nào đã QUÁ HẠN; huy hiệu menu thì đã biết. Người gửi khiếu nại cũng thấy hạn này (minh bạch thời
    // hạn xử lý — Luật BVQLNTD 2023).
    DateTimeOffset? SlaDeadline = null)
{
    /// <summary>MLACP-672: tên đọc được của đối tượng (buổi diễn, vé, phòng trà, khoản ủng hộ…) thay cho mã — null khi đối tượng không còn tồn tại.</summary>
    public string? TargetName { get; init; }
}
