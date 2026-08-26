using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.Commands.HoldTicket;
using MusicLounge.Application.Tickets.Commands.PurchaseTicket;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Queries.GetTicketByQr;

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
    /// tiền = đơn giá × số lượng. Trả kèm URL thanh toán VNPay thật.</summary>
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

    /// <summary>Tra cứu vé bằng mã QR — dùng để quét check-in tại cửa hoặc để chính chủ vé xem lại.
    /// Chỉ chủ vé hoặc Owner/Staff/Admin của đúng venue mới xem được (403 nếu khác), tránh lộ
    /// thông tin buyer/giá/AccessToken livestream cho bất kỳ ai có chuỗi QrCode.</summary>
    [HttpGet("by-qr/{qrCode}")]
    [ProducesResponseType<ApiResponse<TicketDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByQrCode(string qrCode, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTicketByQrQuery(qrCode), ct);
        return Ok(ApiResponse<TicketDetailDto>.Ok(result));
    }
}

public sealed record PurchaseTicketRequest(int HoldId);
