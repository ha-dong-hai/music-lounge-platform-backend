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
}
