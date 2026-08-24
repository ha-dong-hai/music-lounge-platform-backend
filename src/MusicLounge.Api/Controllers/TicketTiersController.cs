using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;
using MusicLounge.Application.TicketTiers.Queries.GetTicketTiers;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (sua/xoa muc gia...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ticket-tiers")]
public sealed class TicketTiersController : ControllerBase
{
    private readonly ISender _sender;

    public TicketTiersController(ISender sender) => _sender = sender;

    /// <summary>Số lượng còn lại tính động từ vé đã bán + hold đang giữ (không dùng cột Sold cố
    /// định — cột đó không được ghi ở đâu trong hệ thống).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TicketTierSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByShow(
        [FromQuery] int showId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTicketTiersQuery(showId), ct);
        return Ok(ApiResponse<IReadOnlyList<TicketTierSummaryDto>>.Ok(result));
    }

    /// <summary>Chỉ thêm được khi sự kiện còn Draft. Tổng TotalCapacity của mọi hạng vé không được
    /// vượt giới hạn vé/event của gói subscription đang hoạt động.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTicketTierCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }
}
