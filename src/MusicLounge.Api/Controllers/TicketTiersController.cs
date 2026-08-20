using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;
using MusicLounge.Application.TicketTiers.Commands.UpdateTicketTier;
using MusicLounge.Application.TicketTiers.Queries.GetTicketTiers;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ticket-tiers")]
public sealed class TicketTiersController : ControllerBase
{
    private readonly ISender _sender;

    public TicketTiersController(ISender sender) => _sender = sender;

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TicketTierSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByShow(
        [FromQuery] int showId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTicketTiersQuery(showId), ct);
        return Ok(ApiResponse<IReadOnlyList<TicketTierSummaryDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTicketTierCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetByShow), new { showId = command.ShowId, version = "1.0" },
            ApiResponse<int>.Ok(id));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateTicketTierRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateTicketTierCommand(id, body.Name, body.Description, body.TotalCapacity), ct);
        return NoContent();
    }
}

public sealed record UpdateTicketTierRequest(string Name, string? Description, int? TotalCapacity);
