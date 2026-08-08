using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Donations.Commands.AcknowledgeDonation;
using MusicLounge.Application.Donations.Commands.ConfirmDonationPaid;
using MusicLounge.Application.Donations.Commands.CreateDonation;
using MusicLounge.Application.Donations.Commands.ProcessDonationPayment;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Application.Donations.Queries.GetMyDonations;
using MusicLounge.Application.Donations.Queries.GetOwnerReceivedDonations;
using MusicLounge.Application.Donations.Queries.GetPendingDonations;
using MusicLounge.Application.Donations.Queries.GetPublicDonationHistory;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/donations")]
public sealed class DonationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly BusinessSettings _settings;

    public DonationsController(ISender sender, IOptions<BusinessSettings> settings)
    {
        _sender = sender;
        _settings = settings.Value;
    }

    /// <summary>W21 Chặng 1 — Audience initiates donation via VNPay.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<DonationInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDonationRequest body,
        CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _sender.Send(
            new CreateDonationCommand(
                body.PerformanceId, body.Amount, body.IsAnonymous,
                body.Message, body.IsMessagePublic, ip), ct);
        // Khong co GET /donations/{id} don le de tro Location toi — dung CreatedAtAction(nameof(Create))
        // se sinh URL vo nghia (tro ve chinh POST action, khong GET duoc). 201 khong kem Location van
        // hop le theo RFC 7231 (SHOULD, khong phai MUST) — donor tra cuu lai qua GET /donations/my.
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DonationInitiationDto>.Ok(result));
    }

    /// <summary>W21 VNPay callback after Audience completes donation payment — redirects to frontend.</summary>
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct = default)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var success = await _sender.Send(new ProcessDonationPaymentCommand(queryParams), ct);
        return success
            ? Redirect(_settings.PaymentSuccessUrl)
            : Redirect(_settings.PaymentFailedUrl);
    }

    /// <summary>Audience — xem lịch sử donate của chính mình (mọi trạng thái).</summary>
    [HttpGet("my")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<PaginatedResult<MyDonationDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyDonations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyDonationsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<MyDonationDto>>.Ok(result));
    }

    /// <summary>Owner — danh sách donation đang chờ xác nhận (PendingOwnerAck).</summary>
    [HttpGet("pending-ack")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<PendingDonationDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingAck(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingDonationsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<PendingDonationDto>>.Ok(result));
    }

    /// <summary>W21 Chặng 2 — donation Owner đã nhận (OwnerReceived), đang chờ chuyển cho nghệ sĩ.</summary>
    [HttpGet("awaiting-payout")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<PendingDonationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAwaitingPayout(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerReceivedDonationsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<PendingDonationDto>>.Ok(result));
    }

    /// <summary>W21 Chặng 1 — Owner xác nhận đã nhận tiền từ VNPay.</summary>
    [HttpPost("{id:int}/acknowledge")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct = default)
    {
        await _sender.Send(new AcknowledgeDonationCommand(id), ct);
        return NoContent();
    }

    /// <summary>W21 Chặng 2 — Owner xác nhận đã chuyển khoản cho nghệ sĩ.</summary>
    [HttpPost("{id:int}/confirm-paid")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmPaid(
        int id,
        [FromBody] ConfirmDonationPaidRequest body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ConfirmDonationPaidCommand(id, body.PaymentRef, body.PaymentEvidenceUrl), ct);
        return NoContent();
    }
}

/// <summary>D17 — Public donation history for a performer (no auth required).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/performers")]
public sealed class PerformerDonationsController : ControllerBase
{
    private readonly ISender _sender;

    public PerformerDonationsController(ISender sender) => _sender = sender;

    [HttpGet("{performerId:int}/donations")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<PaginatedResult<PublicDonationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicHistory(
        int performerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetPublicDonationHistoryQuery(performerId, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<PublicDonationDto>>.Ok(result));
    }
}

public sealed record CreateDonationRequest(
    int PerformanceId,
    decimal Amount,
    bool IsAnonymous = false,
    string? Message = null,
    bool IsMessagePublic = true);

public sealed record ConfirmDonationPaidRequest(string PaymentRef, string? PaymentEvidenceUrl);
