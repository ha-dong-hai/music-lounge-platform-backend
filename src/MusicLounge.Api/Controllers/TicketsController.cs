using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.Commands.HoldTicket;
using MusicLounge.Application.Tickets.Commands.PurchaseTicket;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (purchase, cancel-hold, my-tickets, check-in, transfer...) se chi them
// method vao file nay, khong tao lai.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tickets")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class TicketsController : ControllerBase
{
    private readonly ISender _sender;

    public TicketsController(ISender sender) => _sender = sender;

    /// <summary>Giữ chỗ tạm thời (mặc định 15 phút, cấu hình qua system_config) khi khán giả bắt
    /// đầu checkout — chặn overselling qua khóa phân tán theo ShowId (IShowBookingLock) và kiểm tra
    /// đồng thời 5 lớp quota (mức giá/tier/zone/access-type/subscription cap). Hold hết hạn không
    /// còn tính vào "đã giữ" ngay lập tức (lọc theo ExpiresAt ở mọi query availability), không cần
    /// job dọn dẹp để đúng hành vi "tự động giải phóng".</summary>
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

    /// <summary>Tạo đơn hàng (Payment Pending + vé Pending) từ 1 hold còn hiệu lực, tính đúng tổng
    /// tiền = đơn giá × số lượng. PaymentUrl hiện là placeholder rỗng — tích hợp cổng thanh toán
    /// thật (VNPay) là MLACP-93.</summary>
    [HttpPost("purchase")]
    [ProducesResponseType<ApiResponse<PaymentInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseTicketRequest body, CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _sender.Send(new PurchaseTicketCommand(body.HoldId, ip), ct);
        // Handler da persist 1 ban ghi Payment (Pending) truoc khi tra ve — dung 201 giong
        // Donations.Create (cung ban chat "khoi tao thanh toan", cung persist Payment).
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PaymentInitiationDto>.Ok(result));
    }
}

public sealed record PurchaseTicketRequest(int HoldId);
