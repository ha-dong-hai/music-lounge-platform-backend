using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class UserRepository : Repository<User, Guid>, IUserRepository
{
    private readonly ApplicationDbContext _ctx;

    public UserRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<UserAdminDto>> SearchAsync(
        string? searchText, UserRole? role, bool? isActive,
        int page, int pageSize, CancellationToken ct = default,
        DateTimeOffset? createdFrom = null, DateTimeOffset? createdTo = null)
    {
        var query = _ctx.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var s = searchText.Trim();
            query = query.Where(u =>
                u.Email.Contains(s) || u.FullName.Contains(s) || (u.Phone != null && u.Phone.Contains(s)));
        }

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        // MLACP-598: lọc theo ngày đăng ký. User.CreatedAt là DateTime giờ UTC (AuditableEntity) nên đổi mốc sang UTC rồi
        // so thẳng trong truy vấn — dịch được ở cả SQL Server lẫn SQLite của bộ test.
        if (createdFrom.HasValue)
        {
            var tu = createdFrom.Value.UtcDateTime;
            query = query.Where(u => u.CreatedAt >= tu);
        }
        if (createdTo.HasValue)
        {
            var den = createdTo.Value.UtcDateTime;
            query = query.Where(u => u.CreatedAt <= den);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            // Xem ghi chu ve sap xep theo khoa chinh o FollowRepository.
            .OrderByDescending(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserAdminDto(
                u.Id, u.Email, u.FullName, u.Phone, u.AvatarUrl,
                u.Role.ToString(), u.IsActive, u.EmailVerifiedAt != null, u.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<UserAdminDto>(items, page, pageSize, total);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetKycReviewPageAsync(
        KycReviewStatus status, int page, int pageSize, CancellationToken ct = default)
    {
        // MLACP-505. Trước đây handler nạp ĐẦY ĐỦ mọi người trong hàng đợi (kèm số CCCD, mã số thuế đã mã hoá) rồi mới
        // cắt trang trong bộ nhớ. Giờ hai bước:
        //   1) chỉ lấy KHOÁ SẮP XẾP (Id + hai mốc nộp) của cả hàng đợi — vài chục byte mỗi người;
        //   2) sắp, cắt trang trên khoá, rồi chỉ nạp đầy đủ những người thuộc trang.
        //
        // TRẦN ĐÃ BIẾT: bước sắp vẫn chạy trong bộ nhớ trên tập khoá, vì provider SQLite dùng trong test không ORDER BY
        // được DateTimeOffset (cùng lý do mọi chỗ sắp theo thời gian khác trong repo). Đường nâng cấp: khi test chạy trên
        // SQL Server (Testcontainers) thì đưa OrderBy/Skip/Take vào câu truy vấn ở bước 1, bỏ bước sắp trong bộ nhớ.
        //
        // Thứ tự giữ y như bản cũ: nộp sớm nhất trước, chưa có mốc nộp thì xuống cuối; trùng mốc thì theo Id — bản cũ
        // dựa vào OrderBy ổn định trên thứ tự DB trả về (theo khoá chính), ThenBy(Id) nói điều đó ra thành luật.
        var keys = await _ctx.Users.AsNoTracking()
            .Where(u => u.CitizenCardReviewStatus == status || u.TaxProfileReviewStatus == status)
            .Select(u => new { u.Id, u.CitizenCardSubmittedAt, u.TaxProfileSubmittedAt })
            .ToListAsync(ct);

        var pageIds = keys
            .OrderBy(k => k.CitizenCardSubmittedAt ?? k.TaxProfileSubmittedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(k => k.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => k.Id)
            .ToList();

        var users = await _ctx.Users.AsNoTracking()
            .Where(u => pageIds.Contains(u.Id))
            .ToListAsync(ct);
        var byId = users.ToDictionary(u => u.Id);

        return (pageIds.Select(id => byId[id]).ToList(), keys.Count);
    }
}
