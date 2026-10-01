using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbMenus.Commands.CreateFnbMenu;
using MusicLounge.Application.FnbMenus.Commands.DeleteFnbMenu;
using MusicLounge.Application.FnbMenus.Commands.UpdateFnbMenu;
using MusicLounge.Application.FnbMenus.DTOs;
using MusicLounge.Application.FnbMenus.Queries.GetFnbMenus;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fnb-menus")]
public sealed class FnbMenusController : ControllerBase
{
    private readonly ISender _sender;

    public FnbMenusController(ISender sender) => _sender = sender;

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FnbMenuDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLounge(
        [FromQuery] Guid loungeId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetFnbMenusQuery(loungeId, activeOnly), ct);
        return Ok(ApiResponse<IReadOnlyList<FnbMenuDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFnbMenuCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetByLounge), new { loungeId = command.LoungeId, version = "1.0" },
            ApiResponse<Guid>.Ok(id));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateFnbMenuRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateFnbMenuCommand(
            id, body.Name, body.Description, body.IsActive, body.DisplayOrder), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteFnbMenuCommand(id), ct);
        return NoContent();
    }
}

public sealed record UpdateFnbMenuRequest(string Name, string? Description, bool IsActive, int DisplayOrder);
