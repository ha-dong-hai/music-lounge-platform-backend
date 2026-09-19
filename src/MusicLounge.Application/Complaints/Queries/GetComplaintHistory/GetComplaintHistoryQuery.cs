using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;

namespace MusicLounge.Application.Complaints.Queries.GetComplaintHistory;

/// <summary>
/// MLACP-462. Lịch sử khiếu nại cho Admin.
///
/// Hàng đợi (<c>GET /complaints/pending</c>) chỉ trả khiếu nại chưa xử lý xong, nên sau khi Admin xử lý là khiếu nại
/// biến mất khỏi mọi màn hình: không tra lại được đã quyết gì, cho ai, vì sao — trong khi chính Admin là người phải trả
/// lời nếu người khiếu nại hỏi lại. Giao diện quản trị đã có sẵn bộ lọc trạng thái "đã xử lý/bị từ chối" nhưng không có
/// dữ liệu để hiện.
/// </summary>
/// <param name="Status">
/// Lọc theo trạng thái, nhận NHIỀU giá trị (<c>?status=Resolved&amp;status=Rejected</c>). Bỏ trống = mọi trạng thái.
/// Nhận chuỗi thay vì enum để tên trạng thái sai trả về câu tiếng Việt kèm danh sách hợp lệ, thay vì lỗi ràng buộc kiểu.
/// </param>
public sealed record GetComplaintHistoryQuery(
    string[]? Status,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<ComplaintDto>>;
