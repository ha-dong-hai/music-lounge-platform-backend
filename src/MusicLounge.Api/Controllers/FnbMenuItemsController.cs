using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;
using MusicLounge.Application.FnbMenuItems.Commands.DeleteMenuItem;
using MusicLounge.Application.FnbMenuItems.Commands.UpdateMenuItem;
using MusicLounge.Application.FnbMenuItems.DTOs;
using MusicLounge.Application.FnbMenuItems.Queries.GetMenuItems;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fnb-menu-items")]
public sealed class FnbMenuItemsController : ControllerBase
{
    private readonly ISender _sender;

    public FnbMenuItemsController(ISender sender) => _sender = sender;

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FnbMenuItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByMenu(
        [FromQuery] int menuId, [FromQuery] bool availableOnly = true, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMenuItemsQuery(menuId, availableOnly), ct);
        return Ok(ApiResponse<IReadOnlyList<FnbMenuItemDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMenuItemCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetByMenu), new { menuId = command.MenuId, version = "1.0" },
            ApiResponse<int>.Ok(id));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateMenuItemRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateMenuItemCommand(
            id, body.Category, body.Name, body.Description, body.Price,
            body.ImageUrl, body.IsAvailable, body.DisplayOrder), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteMenuItemCommand(id), ct);
        return NoContent();
    }
}

public sealed record UpdateMenuItemRequest(
    string Category, string Name, string? Description, decimal Price,
    string? ImageUrl, bool IsAvailable, int DisplayOrder);
