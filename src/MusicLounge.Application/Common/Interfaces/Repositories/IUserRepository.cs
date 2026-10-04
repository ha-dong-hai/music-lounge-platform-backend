using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<PaginatedResult<UserAdminDto>> SearchAsync(
        string? searchText, UserRole? role, bool? isActive,
        int page, int pageSize, CancellationToken ct = default,
        DateTimeOffset? createdFrom = null, DateTimeOffset? createdTo = null);

    /// <summary>MLACP-505. Một trang hàng đợi xác minh danh tính: người có giấy tờ tuỳ thân HOẶC hồ sơ thuế đang ở
    /// <paramref name="status"/>, nộp sớm nhất trước. Chỉ nạp đầy đủ những người thuộc trang.</summary>
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetKycReviewPageAsync(
        KycReviewStatus status, int page, int pageSize, CancellationToken ct = default);
}
