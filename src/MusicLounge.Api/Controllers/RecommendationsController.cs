using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.LoungeShows.Queries.GetRecommendedLoungeShows;

namespace MusicLounge.Api.Controllers;

/// <summary>Danh sách buổi diễn gợi ý riêng cho người dùng đang đăng nhập — cá nhân hóa theo sở thích
/// (MLACP-129/130) + hành vi, lấy từ cache do RefreshRecommendationsJob/RefreshUserRecommendationJob
/// tính sẵn (không tính trực tiếp trong request). Chưa bật AiConsent hoặc cache rỗng/hết hạn: trả về
/// show đang thịnh hành (trending) thay vì lỗi hay danh sách trống.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recommendations")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class RecommendationsController : ControllerBase
{
    private readonly ISender _sender;

    public RecommendationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecommendedLoungeShowDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommended(
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetRecommendedLoungeShowsQuery(limit), ct);
        return Ok(ApiResponse<IReadOnlyList<RecommendedLoungeShowDto>>.Ok(result));
    }
}
