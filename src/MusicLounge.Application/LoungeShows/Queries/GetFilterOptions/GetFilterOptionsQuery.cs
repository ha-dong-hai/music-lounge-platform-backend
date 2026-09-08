using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetFilterOptions;

/// <summary>
/// Mọi lựa chọn cho bộ lọc khám phá, trả một lần thay vì bắt client gọi bốn endpoint danh mục rồi
/// tự ghép — và quan trọng hơn: danh sách thành phố phải lấy từ chính các buổi diễn đang có, chứ
/// không phải một danh sách tỉnh thành cứng mà phần lớn không có buổi nào.
/// </summary>
public sealed record GetFilterOptionsQuery : IQuery<FilterOptionsDto>;
