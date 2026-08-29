using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.Commands.FollowLounge;
using MusicLounge.Application.Follows.Commands.UnfollowLounge;
using MusicLounge.Application.Follows.DTOs;
using MusicLounge.Application.Follows.Queries.GetMyFollowedLounges;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/follows")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class FollowsController : ControllerBase
{
    private readonly ISender _sender;

    public FollowsController(ISender sender) => _sender = sender;

    [HttpGet("lounges")]
    [ProducesResponseType<ApiResponse<PaginatedResult<FollowedLoungeDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFollowedLounges(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyFollowedLoungesQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<FollowedLoungeDto>>.Ok(result));
    }

    /// <summary>Follow 1 phòng trà — dùng làm đầu vào tín hiệu gợi ý AI (FollowedVenueBoost) và để
    /// nhận thông báo khi venue có sự kiện mới. 409 nếu đã follow trước đó (không follow trùng).</summary>
    [HttpPost("lounges/{loungeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Follow(int loungeId, CancellationToken ct = default)
    {
        await _sender.Send(new FollowLoungeCommand(loungeId), ct);
        return NoContent();
    }

    [HttpDelete("lounges/{loungeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfollow(int loungeId, CancellationToken ct = default)
    {
        await _sender.Send(new UnfollowLoungeCommand(loungeId), ct);
        return NoContent();
    }
}
