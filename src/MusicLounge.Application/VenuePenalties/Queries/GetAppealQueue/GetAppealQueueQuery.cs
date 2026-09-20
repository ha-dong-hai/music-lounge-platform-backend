using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.VenuePenalties.DTOs;

namespace MusicLounge.Application.VenuePenalties.Queries.GetAppealQueue;

/// <summary>
/// Hàng đợi kháng nghị án phạt đang chờ Admin xử lý.
///
/// <para>Trước task này hệ thống có <c>POST /venue-penalties/{id}/appeal/review</c> để xử lý kháng nghị,
/// nhưng đường LIỆT KÊ duy nhất là <c>GET /venue-penalties/mine</c> — của chủ phòng trà, lọc theo chính
/// người đang đăng nhập. Admin không có cách nào biết phòng trà nào vừa kháng nghị, nên kháng nghị nộp
/// vào rồi nằm im cho tới khi có người tra tay trong cơ sở dữ liệu; mà kháng nghị để quá hạn thì theo
/// quy trình đang chạy sẽ được duyệt tự động.</para>
/// </summary>
/// <param name="Resolved">
/// <c>false</c> (mặc định) là phần việc đang chờ: đã kháng nghị, chưa có quyết định. <c>true</c> để tra lại
/// những kháng nghị đã xử lý.
/// </param>
public sealed record GetAppealQueueQuery(
    bool Resolved = false,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<VenuePenaltyDto>>;
