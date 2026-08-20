using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.Commands.ReviewLivestream;
using MusicLounge.Application.Moderations.Commands.ReviewShow;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Application.Moderations.Queries.GetPendingModerations;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/moderations")]
[Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireAdmin)]
public sealed class EventModerationsController : ControllerBase
{
    private readonly ISender _sender;

    public EventModerationsController(ISender sender) => _sender = sender;

    [HttpGet("pending")]
    [ProducesResponseType<ApiResponse<PaginatedResult<EventModerationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] string? targetType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingModerationsQuery(targetType, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<EventModerationDto>>.Ok(result));
    }

    [HttpPost("livestreams/{livestreamId:int}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewLivestream(
        int livestreamId,
        [FromBody] ReviewLivestreamRequest body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ReviewLivestreamCommand(livestreamId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    [HttpPost("shows/{showId:int}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewShow(
        int showId,
        [FromBody] ReviewLivestreamRequest body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ReviewShowCommand(showId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }
}

public sealed record ReviewLivestreamRequest(string Decision, string? ReviewNote);
