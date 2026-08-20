using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Analytics.Queries.GetOwnerAnalytics;
using MusicLounge.Application.Analytics.Queries.GetPlatformAnalytics;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

/// <summary>CF7 — reporting dashboards for Owner (per-venue) and Admin (platform-wide).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ISender _sender;

    public AnalyticsController(ISender sender) => _sender = sender;

    [HttpGet("my-lounge")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<OwnerAnalyticsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyLounge([FromQuery] int loungeId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerAnalyticsQuery(loungeId), ct);
        return Ok(ApiResponse<OwnerAnalyticsDto>.Ok(result));
    }

    [HttpGet("platform")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<PlatformAnalyticsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlatform(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPlatformAnalyticsQuery(), ct);
        return Ok(ApiResponse<PlatformAnalyticsDto>.Ok(result));
    }
}
