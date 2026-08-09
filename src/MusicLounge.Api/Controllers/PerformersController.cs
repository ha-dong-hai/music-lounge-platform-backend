using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.Commands.CreatePerformer;
using MusicLounge.Application.Performers.Commands.UpdatePerformer;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Application.Performers.Queries.GetPerformerById;
using MusicLounge.Application.Performers.Queries.GetPerformers;

namespace MusicLounge.Api.Controllers;

// §6.12: Performers is a catalog shared across all Owners, not scoped to one venue —
// READ/ASSIGN and CREATE are open to any Owner (or Admin); EDIT is restricted at the handler
// level to created_by_user_id + Admin. Previously the only way a Performer row could ever be
// created was implicitly, inline inside CreateLoungeShowCommandHandler, with no way to look one
// up, edit its profile afterward, or search the catalog before deciding to create a duplicate.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/performers")]
[Authorize(Policy = Policies.RequireOwner)]
public sealed class PerformersController : ControllerBase
{
    private readonly ISender _sender;

    public PerformersController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<PerformerDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPerformersQuery(search, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<PerformerDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse<PerformerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPerformerByIdQuery(id), ct);
        return Ok(ApiResponse<PerformerDto>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePerformerCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, ApiResponse<int>.Ok(id));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdatePerformerRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdatePerformerCommand(
            id, body.Name, body.AvatarUrl, body.Bio, body.Type, body.GenreIds), ct);
        return NoContent();
    }
}

public sealed record UpdatePerformerRequest(
    string Name, string? AvatarUrl, string? Bio, string Type, IReadOnlyList<int> GenreIds);
