using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record LoungeShowListItemDto(
    Guid Id,
    string Name,
    string? CoverImageUrl,
    string LoungeName,
    string LoungeDistrict,
    string LoungeCity,
    DateTimeOffset ScheduledStart,
    LoungeShowFormat Format,
    LoungeShowStatus Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<GenreDto> Genres,
    IReadOnlyList<string> PerformerNames,
    int? OfflineQuota,
    int? OnlineQuota,
    bool? IsWishlisted,
    // MLACP-633: giờ kết thúc mà hệ thống thật sự dùng (ShowSchedule.EffectiveEnd — không khai giờ kết thúc thì
    // bắt đầu + 4 tiếng). Mọi danh sách phải hiện rõ "bắt đầu – kết thúc"; trả mốc hiệu lực chứ không trả cột
    // ScheduledEnd có thể rỗng, để giao diện không phải tự viết lại quy tắc 4 tiếng.
    DateTimeOffset EffectiveEnd);
