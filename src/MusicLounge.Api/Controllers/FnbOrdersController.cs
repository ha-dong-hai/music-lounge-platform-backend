using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;
using MusicLounge.Application.FnbOrders.Commands.InitiateFnbOrderPayment;
using MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;
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
    private readonly BusinessSettings _settings;

    public FnbOrdersController(ISender sender, IOptions<BusinessSettings> settings)
    {
        _sender = sender;
        _settings = settings.Value;
    }

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

    /// <summary>Staff/Owner — hàng đợi đơn F&B của venue, lọc theo trạng thái.</summary>
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

    /// <summary>Staff cập nhật trạng thái đơn: Pending → Preparing → Served → Paid (tuần tự),
    /// hoặc Cancelled (huỷ ngang, chỉ khi chưa Paid).</summary>
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

    /// <summary>Khán giả — khởi tạo thanh toán online qua VNPay cho đơn F&B của chính mình.</summary>
    [HttpPost("{id:int}/pay")]
    [ProducesResponseType<ApiResponse<FnbOrderPaymentInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InitiatePayment(int id, CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _sender.Send(new InitiateFnbOrderPaymentCommand(id, ip), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FnbOrderPaymentInitiationDto>.Ok(result));
    }

    /// <summary>VNPay callback sau khi khán giả hoàn tất thanh toán đơn F&B — chuyển hướng về FE.</summary>
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct = default)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var success = await _sender.Send(new ProcessFnbOrderPaymentCommand(queryParams), ct);
        return success
            ? Redirect(_settings.PaymentSuccessUrl)
            : Redirect(_settings.PaymentFailedUrl);
    }

    // Register this URL (not vnpay-return) as the order's IPN URL in the VNPay merchant portal —
    // see PaymentsController.VnPayIpn for why the browser-redirect endpoint above isn't a reliable
    // substitute for it.
    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    [ProducesResponseType<VnPayIpnResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct = default)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var success = await _sender.Send(new ProcessFnbOrderPaymentCommand(queryParams), ct);
        return Ok(success
            ? new VnPayIpnResponse("00", "Confirm Success")
            : new VnPayIpnResponse("99", "Unknown error"));
    }
}

public sealed record UpdateFnbOrderStatusRequest(string Status);
