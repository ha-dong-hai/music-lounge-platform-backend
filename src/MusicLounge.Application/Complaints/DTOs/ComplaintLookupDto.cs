using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.DTOs;

/// <summary>
/// Những gì một người khiếu nại KHÔNG có tài khoản được phép thấy về khiếu nại của chính họ.
/// Cố ý hẹp: đủ để biết việc của mình đang tới đâu, không lộ thông tin nội bộ hay danh tính ai xử lý
/// — mã tra cứu là thứ duy nhất chứng minh quyền xem, nên nó chỉ nên mở đúng chừng đó.
/// </summary>
public sealed record ComplaintLookupDto(
    int Id,
    string TargetType,
    ComplaintCategory Category,
    ComplaintStatus Status,
    string? Resolution,
    ComplaintResolvedAction? ResolvedAction,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? SlaDeadline);
