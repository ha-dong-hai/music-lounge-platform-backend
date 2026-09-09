using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.LoungeShows.Queries.GetRecommendedLoungeShows;

namespace MusicLounge.Api.Controllers;

/// <summary>Danh sách buổi diễn gợi ý, cá nhân hóa cho bất kỳ ai đang hỏi — kể cả khách chưa đăng
/// nhập.
///
/// Ba mức, tùy lượng thông tin có được về người hỏi: đã đăng nhập + đã bật AiConsent + có kết quả
/// tính sẵn còn hạn thì dùng kết quả đó (đầy đủ nhất, có lọc cộng tác ML.NET và lời giải thích do AI
/// viết); đã đăng nhập thì xếp theo sở thích họ tự khai lúc onboarding và phòng trà đang theo dõi;
/// chưa đăng nhập thì xếp theo ngữ cảnh chính request mang theo và không lưu lại gì.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recommendations")]
[AllowAnonymous]
public sealed class RecommendationsController : ControllerBase
{
    private readonly ISender _sender;

    public RecommendationsController(ISender sender) => _sender = sender;

    /// <param name="recentShowIds">Chỉ dùng cho khách chưa đăng nhập: buổi diễn họ vừa xem, do phía
    /// giao diện tự giữ (localStorage). Máy chủ suy ra gu từ thẻ phân loại của chúng để sắp xếp câu
    /// trả lời, rồi quên đi — không lưu, không đặt cookie, không gắn với định danh nào. Người đã
    /// đăng nhập thì lấy gu từ sở thích tự khai nên tham số này bị bỏ qua.</param>
    /// <param name="genreIds">Cũng chỉ dùng cho khách chưa đăng nhập: thể loại họ vừa chọn.</param>
    [HttpGet]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecommendedLoungeShowDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecommended(
        [FromQuery] int limit = 10,
        [FromQuery] int[]? recentShowIds = null,
        [FromQuery] int[]? genreIds = null,
        [FromQuery] string? city = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetRecommendedLoungeShowsQuery(limit, recentShowIds, genreIds, city), ct);
        return Ok(ApiResponse<IReadOnlyList<RecommendedLoungeShowDto>>.Ok(result));
    }
}
