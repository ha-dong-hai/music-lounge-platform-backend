using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.VenuePenalties.Commands.IssuePenalty;
using MusicLounge.Application.VenuePenalties.Commands.ReviewAppeal;
using MusicLounge.Application.VenuePenalties.Commands.SubmitAppeal;
using MusicLounge.Application.VenuePenalties.DTOs;
using MusicLounge.Application.VenuePenalties.Queries.GetMyVenuePenalties;

namespace MusicLounge.Api.Controllers;

/// <summary>BR-28 — venue penalties (warning/suspension/ban) and appeals (§6.8, §6.17).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/venue-penalties")]
public sealed class VenuePenaltiesController : ControllerBase
{
    private readonly ISender _sender;

    public VenuePenaltiesController(ISender sender) => _sender = sender;

    [HttpPost]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Issue([FromBody] IssuePenaltyRequest body, CancellationToken ct = default)
    {
        var id = await _sender.Send(
            new IssuePenaltyCommand(body.LoungeId, body.PenaltyType, body.Reason, body.EvidenceRef, body.SuspensionDays),
            ct);
        // Khong co GET /venue-penalties/{id} don le — dung CreatedAtAction(nameof(Issue)) se sinh
        // Location vo nghia (tro ve chinh POST action). Owner tra cuu lai qua GET /venue-penalties/mine.
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(id));
    }

    /// <summary>Owner — xem toàn bộ penalty (mọi trạng thái) đã bị áp lên các lounge của mình.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<VenuePenaltyDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyVenuePenaltiesQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<VenuePenaltyDto>>.Ok(result));
    }

    [HttpPost("{id:int}/appeal")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitAppeal(
        int id, [FromBody] SubmitAppealRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SubmitAppealCommand(id, body.AppealReason), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/appeal/review")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReviewAppeal(
        int id, [FromBody] ReviewAppealRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ReviewAppealCommand(id, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }
}

public sealed record IssuePenaltyRequest(int LoungeId, string PenaltyType, string Reason, string? EvidenceRef, int? SuspensionDays);
public sealed record SubmitAppealRequest(string AppealReason);
public sealed record ReviewAppealRequest(string Decision, string? ReviewNote);
