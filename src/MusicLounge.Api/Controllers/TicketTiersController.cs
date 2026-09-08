using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;
using MusicLounge.Application.TicketTiers.Commands.DeleteTicketTier;
using MusicLounge.Application.TicketTiers.Commands.UpdateTicketTier;
using MusicLounge.Application.TicketTiers.Queries.GetTicketTiers;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau se chi them method vao file nay.
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

    /// <summary>Chỉ thêm được khi buổi diễn còn Draft. Tổng TotalCapacity của mọi hạng vé không được
    /// vượt giới hạn vé/event của gói subscription đang hoạt động.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTicketTierCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(id));
    }

    /// <summary>Chỉ sửa được khi buổi diễn còn Draft (422 nếu khác); tăng TotalCapacity vẫn bị kiểm
    /// tra lại giới hạn subscription giống lúc tạo.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateTicketTierRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateTicketTierCommand(id, body.Name, body.Description, body.TotalCapacity), ct);
        return NoContent();
    }

    /// <summary>Xóa thật (hard delete) — chỉ áp dụng khi buổi diễn còn Draft (422 nếu khác).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteTicketTierCommand(id), ct);
        return NoContent();
    }
}

public sealed record UpdateTicketTierRequest(string Name, string? Description, int? TotalCapacity);
