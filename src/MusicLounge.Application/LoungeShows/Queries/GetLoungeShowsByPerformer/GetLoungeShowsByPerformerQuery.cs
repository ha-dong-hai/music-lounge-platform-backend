using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByPerformer;

/// <param name="IncludeEnded">
/// Trang nghệ sĩ có hai mục đích khác nhau: xem sắp diễn ở đâu để mua vé, và xem đã từng diễn
/// những gì. Mặc định chỉ trả buổi sắp diễn vì đó là việc khán giả tới đây để làm.
/// </param>
public sealed record GetLoungeShowsByPerformerQuery(
    int PerformerId, bool IncludeEnded = false, int Page = 1, int PageSize = 10)
    : IQuery<PerformerDetailDto>;
