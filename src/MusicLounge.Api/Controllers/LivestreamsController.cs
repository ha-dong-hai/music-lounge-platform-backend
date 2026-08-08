using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Livestreams.Commands.CreateLivestream;
using MusicLounge.Application.Livestreams.Commands.EndLivestream;
using MusicLounge.Application.Livestreams.Commands.StartLivestream;
using MusicLounge.Application.Livestreams.Commands.TerminateLivestream;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Application.Livestreams.Queries.GetChatHistory;
using MusicLounge.Application.Livestreams.Queries.GetLivestreamCredentials;
using MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/livestreams")]
public sealed class LivestreamsController : ControllerBase
{
    private readonly ISender _sender;

    public LivestreamsController(ISender sender) => _sender = sender;

    [HttpGet("{id:int}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<LivestreamDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetLivestreamDetailQuery(id), ct);
        return Ok(ApiResponse<LivestreamDetailDto>.Ok(result));
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateLivestreamCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1" }, ApiResponse<int>.Ok(id));
    }

    [HttpPost("{id:int}/start")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        await _sender.Send(new StartLivestreamCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/end")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> End(int id, CancellationToken ct)
    {
        await _sender.Send(new EndLivestreamCommand(id), ct);
        return NoContent();
    }

    /// <summary>Staff/Admin của venue xem RTMP URL + Stream Key để cắm OBS. Không lộ ra viewer.</summary>
    [HttpGet("{id:int}/credentials")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType<ApiResponse<LivestreamCredentialsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentials(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLivestreamCredentialsQuery(id), ct);
        return Ok(ApiResponse<LivestreamCredentialsDto>.Ok(result));
    }

    [HttpGet("{id:int}/chat")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ChatMessageDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChatHistory(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetChatHistoryQuery(id, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ChatMessageDto>>.Ok(result));
    }

    /// <summary>W22 — Admin terminates a live stream for policy violations.</summary>
    [HttpPost("{id:int}/terminate")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Terminate(
        int id, [FromBody] TerminateLivestreamRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new TerminateLivestreamCommand(id, body.Reason), ct);
        return NoContent();
    }
}

public sealed record TerminateLivestreamRequest(string Reason);
