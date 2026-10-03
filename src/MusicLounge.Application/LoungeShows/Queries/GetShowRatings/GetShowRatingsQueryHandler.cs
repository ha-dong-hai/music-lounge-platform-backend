using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetShowRatings;

internal sealed class GetShowRatingsQueryHandler : IRequestHandler<GetShowRatingsQuery, ShowRatingsDto>
{
    private readonly IUnitOfWork _uow;

    public GetShowRatingsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<ShowRatingsDto> Handle(GetShowRatingsQuery request, CancellationToken ct)
    {
        var showExists = await _uow.Repository<LoungeShow, Guid>().AnyAsync(s => s.Id == request.ShowId, ct);
        if (!showExists)
            throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        // DONE WHEN: "Đánh giá bị gỡ không hiển thị" — loại IsRemoved ngay từ đầu, không tính vào
        // điểm trung bình lẫn phân bố sao, cùng quy ước đã dùng ở GetOwnerAnalyticsQueryHandler/
        // GetLoungeShowDetailQueryHandler.
        var ratings = await _uow.Repository<LoungeShowRating, Guid>()
            .FindAsync(r => r.LoungeShowId == request.ShowId && !r.IsRemoved, ct);

        var totalCount = ratings.Count;
        var averageScore = totalCount > 0 ? (decimal?)Math.Round(ratings.Average(r => r.Score), 2) : null;

        var distribution = Enumerable.Range(1, 5)
            .ToDictionary(score => score, score => ratings.Count(r => r.Score == score));

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // MLACP-573: lọc theo số sao chỉ áp vào DANH SÁCH nhận xét (và tổng số của trang) — tổng quan ở trên giữ nguyên.
        var locTheoSao = request.Score.HasValue
            ? ratings.Where(r => r.Score == request.Score.Value).ToList()
            : ratings;
        var trang = locTheoSao
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // LoungeShowRating.UserId co the null (DSAR erasure anonymize User row, khong xoa Rating) —
        // chi load User cho nhung rating con UserId that. MLACP-573: chi nap nguoi viet cua TRANG dang tra, khong phai
        // cua moi danh gia (truoc day nap ca nghin User de in 20 ten).
        var userIds = trang.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = userIds.Count > 0
            ? await _uow.Repository<User, Guid>().FindAsync(u => userIds.Contains(u.Id), ct)
            : [];
        var userById = users.ToDictionary(u => u.Id);

        var items = trang
            .Select(r => new ShowRatingItemDto(
                r.Id,
                r.UserId,
                r.UserId.HasValue && userById.TryGetValue(r.UserId.Value, out var user) ? user.FullName : null,
                r.Score,
                // MLACP-574: PublicComment — lời bình đang bị ẩn tạm chờ Admin thì không trả chữ, chỉ báo cờ.
                r.PublicComment,
                r.CreatedAt,
                r.CommentHiddenAt is not null))
            .ToList();

        return new ShowRatingsDto(
            averageScore,
            totalCount,
            distribution,
            new PaginatedResult<ShowRatingItemDto>(items, page, pageSize, locTheoSao.Count));
    }
}
