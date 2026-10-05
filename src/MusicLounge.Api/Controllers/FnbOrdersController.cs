using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.FnbOrders;
using MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;
using MusicLounge.Application.FnbOrders.Commands.InitiateFnbOrderPayment;
using MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;
using MusicLounge.Application.FnbOrders.Commands.UpdateFnbOrderStatus;
using MusicLounge.Application.FnbOrders.DTOs;
using MusicLounge.Application.FnbOrders.Queries.GetFnbOrders;
using MusicLounge.Application.FnbOrders.Queries.GetMyFnbOrders;
using MusicLounge.Application.FnbOrders.Queries.GetMyGuestSeat;

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
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFnbOrderCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetByLounge), new { loungeId = command.LoungeId, version = "1.0" },
            ApiResponse<Guid>.Ok(id));
    }

    /// <summary>Khán giả — đơn F&amp;B của chính mình, mới nhất trước (MLACP-357). Mỗi đơn có
    /// `Status` (bếp đã làm tới đâu) tách khỏi `IsPaid` (đã trả tiền chưa), và `OnlinePaymentLiveUntil`
    /// nếu đang có một link VNPay còn trả được. Đơn nhân viên tạo hộ khách vãng lai không có trong
    /// danh sách của ai. `loungeId` (tuỳ chọn, MLACP-500): chỉ đơn ở phòng trà đó.</summary>
    [HttpGet("my")]
    [ProducesResponseType<ApiResponse<PaginatedResult<FnbOrderDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? loungeId = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyFnbOrdersQuery(page, pageSize, loungeId), ct);
        return Ok(ApiResponse<PaginatedResult<FnbOrderDto>>.Ok(result));
    }

    /// <summary>MLACP-630: khu mà người đang đăng nhập ngồi ở phòng trà này, suy từ vé vào cửa còn hiệu lực của họ cho
    /// buổi đang diễn (hoặc mở cửa trong 3 giờ tới). `data` là null khi không suy ra được (không có vé đêm đó, vé xem
    /// trực tuyến, hạng vé chưa gắn khu). Đơn khách tự tạo qua POST /fnb-orders mà không gửi `zoneId` sẽ được ghi đúng
    /// khu này — client gọi endpoint này để CHO KHÁCH XEM trước khi gửi đơn.</summary>
    [HttpGet("my-seat")]
    [ProducesResponseType<ApiResponse<GuestSeatDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySeat([FromQuery] Guid loungeId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyGuestSeatQuery(loungeId), ct);
        return Ok(ApiResponse<GuestSeatDto?>.Ok(result));
    }

    /// <summary>Staff/Owner — hàng đợi đơn F&B của venue, lọc theo trạng thái. Sắp **mới nhất trước**
    /// (MLACP-411); màn bếp/bar muốn đơn cũ lên đầu thì tự sắp lại phía client.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<FnbOrderDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByLounge(
        [FromQuery] Guid loungeId, [FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetFnbOrdersQuery(loungeId, status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<FnbOrderDto>>.Ok(result));
    }

    /// <summary>Staff cập nhật trạng thái đơn: Pending → Preparing → Served → Paid (tuần tự),
    /// hoặc Cancelled (huỷ ngang, chỉ khi chưa Paid).</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateFnbOrderStatusRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateFnbOrderStatusCommand(id, body.Status), ct);
        return NoContent();
    }

    /// <summary>Khán giả — khởi tạo thanh toán online qua VNPay cho đơn F&B của chính mình.</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType<ApiResponse<FnbOrderPaymentInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InitiatePayment(Guid id, CancellationToken ct = default)
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
        var outcome = await _sender.Send(new ProcessFnbOrderPaymentCommand(queryParams), ct);
        return Redirect(VnPayIpnProtocol.BuyerLandingUrl(outcome, _settings));
    }

    // MLACP-394: URL IPN dang ky voi VNPay la /payments/vnpay/ipn — mot URL cho moi luong, vi VNPay gan IPN theo
    // terminal. Endpoint rieng nay giu lai de khong pha ket noi da co; no chay dung command nhu URL chung.
    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    [ProducesResponseType<VnPayIpnResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct = default)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var outcome = await _sender.Send(new ProcessFnbOrderPaymentCommand(queryParams), ct);
        // MLACP-334. Truoc day moi thu khong phai thanh cong deu tra 99 "Unknown error", ma
        // theo tai lieu VNPay la ma RETRY DUOC — nen mot callback trung lap binh thuong hay mot
        // chu ky gia mao cung khien VNPay goi lai du 10 lan trong ~50 phut. Bang map o VnPayIpnProtocol.
        var (rspCode, message) = VnPayIpnProtocol.ResponseFor(outcome);
        return Ok(new VnPayIpnResponse(rspCode, message));
    }
}

public sealed record UpdateFnbOrderStatusRequest(string Status);
