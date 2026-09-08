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
        var showExists = await _uow.Repository<LoungeShow, int>().AnyAsync(s => s.Id == request.ShowId, ct);
        if (!showExists)
            throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        // DONE WHEN: "Đánh giá bị gỡ không hiển thị" — loại IsRemoved ngay từ đầu, không tính vào
        // điểm trung bình lẫn phân bố sao, cùng quy ước đã dùng ở GetOwnerAnalyticsQueryHandler/
        // GetLoungeShowDetailQueryHandler.
        var ratings = await _uow.Repository<LoungeShowRating, int>()
            .FindAsync(r => r.LoungeShowId == request.ShowId && !r.IsRemoved, ct);

        var totalCount = ratings.Count;
        var averageScore = totalCount > 0 ? (decimal?)Math.Round(ratings.Average(r => r.Score), 2) : null;

        var distribution = Enumerable.Range(1, 5)
            .ToDictionary(score => score, score => ratings.Count(r => r.Score == score));

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // LoungeShowRating.UserId co the null (DSAR erasure anonymize User row, khong xoa Rating) —
        // chi load User cho nhung rating con UserId that.
        var userIds = ratings.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = userIds.Count > 0
            ? await _uow.Repository<User, int>().FindAsync(u => userIds.Contains(u.Id), ct)
            : [];
        var userById = users.ToDictionary(u => u.Id);

        var items = ratings
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ShowRatingItemDto(
                r.Id,
                r.UserId,
                r.UserId.HasValue && userById.TryGetValue(r.UserId.Value, out var user) ? user.FullName : null,
                r.Score,
                r.Comment,
                r.CreatedAt))
            .ToList();

        return new ShowRatingsDto(
            averageScore,
            totalCount,
            distribution,
            new PaginatedResult<ShowRatingItemDto>(items, page, pageSize, totalCount));
    }
}
