using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

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
        // Khong co GET /fnb-orders/{id} don le de tro Location toi (xem hang doi Staff/lich su don
        // cua khan gia se them o task khac) — 201 khong kem Location van hop le theo RFC 7231.
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(id));
    }
}
