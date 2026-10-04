using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class ComplaintRepository : Repository<Complaint, Guid>, IComplaintRepository
{
    private readonly ApplicationDbContext _ctx;

    public ComplaintRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<ComplaintDto>> GetMyComplaintsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Complaints.AsNoTracking().Where(c => c.ComplainantUserId == userId);
        return await ProjectPageAsync(query, page, pageSize, ct);
    }

    public async Task<PaginatedResult<ComplaintDto>> GetPendingAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Complaints.AsNoTracking()
            .Where(c => c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Investigating);
        return await ProjectPageAsync(query, page, pageSize, ct, cuNhatTruoc: true);
    }

    /// <summary>
    /// MLACP-462. Toàn bộ khiếu nại cho Admin, lọc theo trạng thái. Khác <see cref="GetPendingAsync"/> ở chỗ đó là HÀNG ĐỢI
    /// (chỉ việc chưa xong), còn đây là LỊCH SỬ: không truyền trạng thái nào thì trả tất cả.
    ///
    /// Trước đây không có đường nào xem khiếu nại ĐÃ xử lý, nên Admin xử lý xong là mất dấu — không tra lại được đã quyết
    /// gì cho ai, trong khi chính họ là người phải trả lời nếu người khiếu nại hỏi lại.
    /// </summary>
    public async Task<PaginatedResult<ComplaintDto>> GetHistoryAsync(
        IReadOnlyList<ComplaintStatus> statuses, string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Complaints.AsNoTracking();
        if (statuses.Count > 0) query = query.Where(c => statuses.Contains(c.Status));
        // MLACP-502: tìm trước khi phân trang. Chỉ là điều kiện LỌC — DTO trả về vẫn y như cũ, không đổi cách hiển thị
        // SĐT. MLACP-515: mã khiếu nại giờ là GUID — gõ đúng một GUID thì khớp ĐÚNG mã; toàn chữ số là một phần SĐT
        // (không so với nội dung, vì chuỗi số ngắn là chuỗi con của gần như mọi mô tả).
        if (keyword is not null)
        {
            if (Guid.TryParse(keyword, out var ma))
                query = query.Where(c => c.Id == ma);
            else if (keyword.All(char.IsDigit))
                query = query.Where(c => c.ContactPhone != null && c.ContactPhone.Contains(keyword));
            else
                query = query.Where(c => c.Description.ToLower().Contains(keyword)
                                      || (c.ContactPhone != null && c.ContactPhone.Contains(keyword)));
        }
        // MLACP-597: Admin chỉ lọc các trạng thái CHƯA xong (Open/Investigating) thì đó là HÀNG CHỜ, không phải lịch sử —
        // xếp cũ nhất trước như mọi hàng đợi duyệt khác của hệ thống (hoàn tiền, định danh, tài khoản nhận tiền, kháng
        // nghị). Trước đây trang quản trị dùng endpoint này cho cả tab "chờ xử lý" nên khiếu nại vừa gửi nằm trên đầu,
        // khiếu nại chờ lâu nhất bị đẩy xuống trang sau. Có lẫn trạng thái đã xong hoặc không lọc → vẫn mới nhất trước.
        var chiViecChuaXong = statuses.Count > 0
            && statuses.All(s => s is ComplaintStatus.Open or ComplaintStatus.Investigating);
        return await ProjectPageAsync(query, page, pageSize, ct, cuNhatTruoc: chiViecChuaXong);
    }

    /// <param name="cuNhatTruoc">Hàng chờ → cũ nhất trước. Mặc định mới nhất trước (lịch sử, "khiếu nại của tôi").
    /// Xếp theo Id: khoá chính là GUID có thứ tự thời gian (OrderedGuid) nên trùng thứ tự tạo, và tránh được việc so
    /// <c>DateTimeOffset</c> mà provider SQLite của bộ test không dịch được.</param>
    private static async Task<PaginatedResult<ComplaintDto>> ProjectPageAsync(
        IQueryable<Complaint> query, int page, int pageSize, CancellationToken ct, bool cuNhatTruoc = false)
    {
        var total = await query.CountAsync(ct);
        var items = await (cuNhatTruoc ? query.OrderBy(c => c.Id) : query.OrderByDescending(c => c.Id))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ComplaintDto(
                c.Id,
                c.TargetType,
                c.TargetId,
                c.Category,
                c.Description,
                c.EvidenceUrls,
                c.ContactPhone,
                c.Status,
                c.Complainant != null ? c.Complainant.FullName : null,
                c.Admin != null ? c.Admin.FullName : null,
                c.Resolution,
                c.ResolvedAction,
                c.ResolvedAt,
                c.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<ComplaintDto>(items, page, pageSize, total);
    }
}
