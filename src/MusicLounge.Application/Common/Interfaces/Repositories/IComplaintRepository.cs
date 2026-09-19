using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IComplaintRepository : IRepository<Complaint, int>
{
    Task<PaginatedResult<ComplaintDto>> GetMyComplaintsAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);

    Task<PaginatedResult<ComplaintDto>> GetPendingAsync(
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>MLACP-462: lịch sử khiếu nại cho Admin — danh sách rỗng nghĩa là không lọc, trả mọi trạng thái.</summary>
    Task<PaginatedResult<ComplaintDto>> GetHistoryAsync(
        IReadOnlyList<MusicLounge.Domain.Enums.ComplaintStatus> statuses, int page, int pageSize, CancellationToken ct = default);
}
