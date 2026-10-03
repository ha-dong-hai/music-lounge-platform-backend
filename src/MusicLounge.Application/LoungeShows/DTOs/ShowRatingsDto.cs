using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record ShowRatingItemDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    int Score,
    string? Comment,
    DateTimeOffset CreatedAt,
    // MLACP-574: true = có lời bình nhưng đang ẩn tạm chờ kiểm duyệt (Comment trả null). Số sao vẫn tính bình thường.
    bool CommentHidden = false);

public sealed record ShowRatingsDto(
    decimal? AverageScore,
    int TotalCount,
    IReadOnlyDictionary<int, int> ScoreDistribution,
    PaginatedResult<ShowRatingItemDto> Items);
