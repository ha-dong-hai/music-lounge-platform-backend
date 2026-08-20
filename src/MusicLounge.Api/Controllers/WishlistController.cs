using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.Commands.AddToWishlist;
using MusicLounge.Application.LoungeShows.Commands.RemoveFromWishlist;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.LoungeShows.Queries.GetUserWishlist;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wishlist")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class WishlistController : ControllerBase
{
    private readonly ISender _sender;

    public WishlistController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyWishlist(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetUserWishlistQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpPost("{showId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Add(int showId, CancellationToken ct = default)
    {
        await _sender.Send(new AddToWishlistCommand(showId), ct);
        return NoContent();
    }

    [HttpDelete("{showId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove(int showId, CancellationToken ct = default)
    {
        await _sender.Send(new RemoveFromWishlistCommand(showId), ct);
        return NoContent();
    }
}
