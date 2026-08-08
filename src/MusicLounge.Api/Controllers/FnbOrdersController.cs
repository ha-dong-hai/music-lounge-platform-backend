using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;
using MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;
using MusicLounge.Application.FnbOrders.DTOs;
using MusicLounge.Application.FnbOrders.Queries.GetFnbOrders;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fnb-orders")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class FnbOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public FnbOrdersController(ISender sender) => _sender = sender;

    [HttpPost]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFnbOrderCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetByLounge), new { loungeId = command.LoungeId, version = "1.0" },
            ApiResponse<int>.Ok(id));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id, [FromBody] UpdateFnbOrderStatusRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateFnbOrderStatusCommand(id, body.Status), ct);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<FnbOrderDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByLounge(
        [FromQuery] int loungeId, [FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetFnbOrdersQuery(loungeId, status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<FnbOrderDto>>.Ok(result));
    }
}

public sealed record UpdateFnbOrderStatusRequest(string Status);
