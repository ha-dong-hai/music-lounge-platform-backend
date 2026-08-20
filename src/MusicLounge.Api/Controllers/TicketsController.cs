using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.Commands.AcceptTicketTransfer;
using MusicLounge.Application.Tickets.Commands.CancelHold;
using MusicLounge.Application.Tickets.Commands.CancelTicket;
using MusicLounge.Application.Tickets.Commands.CancelTicketTransfer;
using MusicLounge.Application.Tickets.Commands.CheckInTicket;
using MusicLounge.Application.Tickets.Commands.HoldTicket;
using MusicLounge.Application.Tickets.Commands.InitiateTicketTransfer;
using MusicLounge.Application.Tickets.Commands.PurchaseTicket;
using MusicLounge.Application.Tickets.Commands.SellWalkInTicket;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Application.Refunds.Queries.GetMyRefundRequests;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Queries.GetIncomingTicketTransfers;
using MusicLounge.Application.Tickets.Queries.GetMyTickets;
using MusicLounge.Application.Tickets.Queries.GetTicketDetail;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tickets")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class TicketsController : ControllerBase
{
    private readonly ISender _sender;

    public TicketsController(ISender sender) => _sender = sender;

    [HttpPost("holds")]
    [ProducesResponseType<ApiResponse<HoldTicketResultDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Hold(
        [FromBody] HoldTicketCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<HoldTicketResultDto>.Ok(result));
    }

    [HttpPost("purchase")]
    [ProducesResponseType<ApiResponse<PaymentInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseTicketRequest body, CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _sender.Send(
            new PurchaseTicketCommand(body.HoldId, ip), ct);
        // Handler da persist 1 ban ghi Payment (Pending) truoc khi tra ve — dung 201 giong
        // Donations.Create (cung ban chat "khoi tao thanh toan", cung persist Payment).
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PaymentInitiationDto>.Ok(result));
    }

    [HttpDelete("holds/{holdId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelHold(int holdId, CancellationToken ct = default)
    {
        await _sender.Send(new CancelHoldCommand(holdId), ct);
        return NoContent();
    }

    [HttpGet("my")]
    [ProducesResponseType<ApiResponse<PaginatedResult<TicketListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyTicketsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<TicketListItemDto>>.Ok(result));
    }

    /// <summary>Danh sách yêu cầu hoàn tiền của chính user (mọi trạng thái) — để tự tra kết quả sau khi hủy vé Confirmed.</summary>
    [HttpGet("refund-requests/my")]
    [ProducesResponseType<ApiResponse<PaginatedResult<RefundRequestDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyRefundRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyRefundRequestsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<RefundRequestDto>>.Ok(result));
    }

    [HttpGet("incoming-transfers")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<IncomingTicketTransferDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIncomingTransfers(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetIncomingTicketTransfersQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<IncomingTicketTransferDto>>.Ok(result));
    }

    [HttpPost("walk-in")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType<ApiResponse<WalkInSaleResultDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SellWalkIn(
        [FromBody] SellWalkInTicketCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<WalkInSaleResultDto>.Ok(result));
    }

    /// <summary>W15 — Staff quét QR để check-in vé Physical tại cửa (D6-scoped theo LoungeId của staff).</summary>
    [HttpPost("check-in")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType<ApiResponse<TicketDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInTicketRequest body, CancellationToken ct = default)
    {
        var result = await _sender.Send(new CheckInTicketCommand(body.QrCode), ct);
        return Ok(ApiResponse<TicketDetailDto>.Ok(result));
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var refundRequestId = await _sender.Send(new CancelTicketCommand(id), ct);
        return Ok(ApiResponse<int>.Ok(refundRequestId));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<TicketDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTicketDetailQuery(id), ct);
        return Ok(ApiResponse<TicketDetailDto>.Ok(result));
    }

    [HttpPost("{id:guid}/transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitiateTransfer(
        Guid id, [FromBody] InitiateTicketTransferRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new InitiateTicketTransferCommand(id, body.RecipientEmail), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptTransfer(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new AcceptTicketTransferCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelTransfer(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new CancelTicketTransferCommand(id), ct);
        return NoContent();
    }
}

public sealed record PurchaseTicketRequest(int HoldId);

public sealed record CheckInTicketRequest(string QrCode);

public sealed record InitiateTicketTransferRequest(string RecipientEmail);
