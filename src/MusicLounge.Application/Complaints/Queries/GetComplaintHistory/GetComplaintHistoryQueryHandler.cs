using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Complaints.Queries.GetComplaintHistory;

internal sealed class GetComplaintHistoryQueryHandler
    : IRequestHandler<GetComplaintHistoryQuery, PaginatedResult<ComplaintDto>>
{
    private readonly IComplaintRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetComplaintHistoryQueryHandler> _logger;

    public GetComplaintHistoryQueryHandler(
        IComplaintRepository repo,
        ICurrentUserService currentUser,
        ILogger<GetComplaintHistoryQueryHandler> logger)
    {
        _repo = repo;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PaginatedResult<ComplaintDto>> Handle(
        GetComplaintHistoryQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var statuses = new List<ComplaintStatus>();
        foreach (var ten in request.Status ?? [])
        {
            if (string.IsNullOrWhiteSpace(ten)) continue;
            if (!Enum.TryParse<ComplaintStatus>(ten.Trim(), ignoreCase: true, out var trangThai))
                throw new DomainException(
                    $"Trạng thái \"{ten}\" không hợp lệ. Dùng một trong: {string.Join(", ", Enum.GetNames<ComplaintStatus>())}.");
            if (!statuses.Contains(trangThai)) statuses.Add(trangThai);
        }

        var result = await _repo.GetHistoryAsync(statuses, page, size, ct);

        // MLACP-462: GHI LẠI AI ĐÃ XEM. Khiếu nại chứa mô tả sự việc và SỐ ĐIỆN THOẠI của người khiếu nại, kể cả khách
        // không có tài khoản. Hàng đợi chỉ trả việc chưa xử lý nên lượng dữ liệu cá nhân đọc được có giới hạn tự nhiên;
        // lịch sử thì mở toàn bộ kho đó cho vai trò Admin. Vẫn đúng vai trò của họ — nhưng phải có dấu vết, để khi cần
        // còn trả lời được câu "ai đã đọc dữ liệu của tôi" (BVDLCN 2025). Chỉ ghi số lượng và bộ lọc, KHÔNG ghi nội dung.
        _logger.LogInformation(
            "Admin {AdminUserId} xem lịch sử khiếu nại: trạng thái={Statuses} trang={Page} cỡ={PageSize} → {Count}/{Total} bản ghi",
            _currentUser.UserId,
            statuses.Count == 0 ? "tất cả" : string.Join("|", statuses),
            page, size, result.Items.Count, result.TotalCount);

        return result;
    }
}
