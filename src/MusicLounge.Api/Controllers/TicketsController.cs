using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Application.Refunds.Queries.GetMyRefundRequests;
using MusicLounge.Application.Tickets.Commands.AcceptTicketTransfer;
using MusicLounge.Application.Tickets.Commands.CancelTicket;
using MusicLounge.Application.Tickets.Commands.CancelTicketTransfer;
using MusicLounge.Application.Tickets.Commands.CheckInTicket;
using MusicLounge.Application.Tickets.Commands.HoldTicket;
using MusicLounge.Application.Tickets.Commands.InitiateTicketTransfer;
using MusicLounge.Application.Tickets.Commands.PurchaseTicket;
using MusicLounge.Application.Tickets.Commands.SellWalkInTicket;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Queries.GetIncomingTicketTransfers;
using MusicLounge.Application.Tickets.Queries.GetMyTickets;
using MusicLounge.Application.Tickets.Queries.GetTicketByQr;
using MusicLounge.Application.Tickets.Queries.GetTicketDetail;
using MusicLounge.Domain.Enums;

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

    /// <summary>Toàn bộ vé đã mua của user đang đăng nhập, sắp xếp theo thời gian mua mới nhất
    /// trước — lọc theo trạng thái qua query param `status` nếu có (Pending/Confirmed/Used/
    /// Cancelled/Refunded).</summary>
    [HttpGet("my")]
    [ProducesResponseType<ApiResponse<PaginatedResult<TicketListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] TicketStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyTicketsQuery(status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<TicketListItemDto>>.Ok(result));
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

    /// <summary>Chi tiết đầy đủ 1 vé — QR code, thông tin buổi diễn, khu vực/chỗ ngồi (nếu Physical),
    /// trạng thái. Chỉ chính chủ vé xem được (403 nếu khác).</summary>
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

    /// <summary>Hủy vé theo chính sách hủy của event (CancellationAllowed/CancellationDeadlineHours/
    /// RefundPercentage) — chỉ chính chủ vé được hủy (403 nếu khác). Vé Pending (chưa từng thanh
    /// toán thật) hủy ngay không áp dụng chính sách. Vé Confirmed tạo `RefundRequest` (Pending) với
    /// số tiền hoàn theo đúng % quy định của event, trả về id yêu cầu hoàn tiền (0 nếu là vé
    /// Pending, không tạo yêu cầu hoàn tiền).</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        var refundRequestId = await _sender.Send(new CancelTicketCommand(id), ct);
        return Ok(ApiResponse<int>.Ok(refundRequestId));
    }

    /// <summary>Bán vé vật lý tại quầy (Staff/Owner của đúng venue) — thanh toán Cash, xác nhận
    /// ngay, không qua flow hold/VNPay. Chịu chung mọi giới hạn quota (mức giá/tier/zone/
    /// access-type/subscription cap) như đường mua online, khóa theo show để tránh bán vượt khi
    /// nhiều quầy/nhiều request cùng bán lúc gần hết vé.</summary>
    [HttpPost("walk-in")]
    [ProducesResponseType<ApiResponse<WalkInSaleResultDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SellWalkIn(
        [FromBody] SellWalkInTicketCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<WalkInSaleResultDto>.Ok(result));
    }

    /// <summary>Check-in vé vật lý tại cửa bằng mã QR (Staff/Owner của đúng venue) — chỉ áp dụng khi
    /// show đang Ongoing, vé Confirmed và chưa từng check-in; khóa theo QrCode để cùng 1 vé bị quét
    /// 2 cửa cùng lúc không tạo ra 2 lượt check-in.</summary>
    [HttpPost("check-in")]
    [ProducesResponseType<ApiResponse<TicketDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInTicketCommand command, CancellationToken ct = default)
    {
        var result = await _sender.Send(command, ct);
        return Ok(ApiResponse<TicketDetailDto>.Ok(result));
    }

    /// <summary>Lịch sử yêu cầu hoàn tiền của chính người dùng đang đăng nhập — chỗ duy nhất để
    /// khán giả tự tra lại kết quả yêu cầu hoàn tiền đã tạo qua Cancel (Admin-only
    /// /admin/refund-requests không dùng được cho vai trò này).</summary>
    [HttpGet("refund-requests/my")]
    [ProducesResponseType<ApiResponse<PaginatedResult<RefundRequestDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyRefundRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyRefundRequestsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<RefundRequestDto>>.Ok(result));
    }

    // ---- Chuyển nhượng vé ----

    /// <summary>Khởi tạo chuyển nhượng vé cho người nhận qua email — chỉ chủ vé, vé phải Confirmed,
    /// show chưa Ended/Cancelled, vé chưa check-in/chưa dùng livestream, chưa có yêu cầu chuyển
    /// nhượng nào khác đang chờ. Tự động hết hạn sau 48h nếu người nhận không phản hồi
    /// (TicketTransferExpiryJob).</summary>
    [HttpPost("{id:guid}/transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> InitiateTransfer(
        Guid id, [FromBody] InitiateTransferRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new InitiateTicketTransferCommand(id, body.RecipientEmail), ct);
        return NoContent();
    }

    /// <summary>Danh sách vé đang được chuyển nhượng đến người dùng đang đăng nhập, chờ chấp nhận.</summary>
    [HttpGet("incoming-transfers")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<IncomingTicketTransferDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIncomingTransfers(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetIncomingTicketTransfersQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<IncomingTicketTransferDto>>.Ok(result));
    }

    /// <summary>Người nhận chấp nhận chuyển nhượng — chuyển quyền sở hữu vé, cấp lại mã QR mới (vô
    /// hiệu hóa mã cũ) và access token livestream mới nếu có.</summary>
    [HttpPost("{id:guid}/transfer/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AcceptTransfer(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new AcceptTicketTransferCommand(id), ct);
        return NoContent();
    }

    /// <summary>Hủy chuyển nhượng — người gửi hủy trước khi được nhận, hoặc người nhận từ chối.</summary>
    [HttpPost("{id:guid}/transfer/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelTransfer(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new CancelTicketTransferCommand(id), ct);
        return NoContent();
    }
}

public sealed record InitiateTransferRequest(string RecipientEmail);

public sealed record PurchaseTicketRequest(int HoldId);
