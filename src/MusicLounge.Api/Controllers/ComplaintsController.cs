using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.Commands.CreateComplaint;
using MusicLounge.Application.Complaints.Commands.ResolveComplaint;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Application.Complaints.Queries.LookupComplaint;
using MusicLounge.Application.Complaints.Queries.GetMyComplaints;
using MusicLounge.Application.Complaints.Queries.GetPendingComplaints;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/complaints")]
public sealed class ComplaintsController : ControllerBase
{
    private readonly ISender _sender;

    public ComplaintsController(ISender sender) => _sender = sender;

    /// <summary>Gửi khiếu nại/báo cáo vi phạm — cho phép cả khách chưa đăng nhập (D17, phải để lại
    /// số điện thoại liên hệ nếu vậy). TargetType: show/venue/donation/ticket/penalty/livestream.
    /// Vào hàng đợi Admin (GET pending) với SLA (system_config: complaint_sla_hours).</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateComplaintCommand command, CancellationToken ct = default)
    {
        var created = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ComplaintCreatedDto>.Ok(created));
    }

    /// <summary>Tra cứu khiếu nại bằng mã nhận được lúc gửi — dành cho người KHÔNG có tài khoản.
    /// Trước đây họ gửi khiếu nại xong chỉ nhận về một số id và không có cách nào biết kết quả:
    /// /complaints/my đòi đăng nhập, không có endpoint tra cứu, và không có SMS báo kết quả. Trả về
    /// cùng một thông báo cho mã sai lẫn mã không tồn tại, vì đây là endpoint công khai và phân biệt
    /// hai trường hợp sẽ biến nó thành công cụ dò mã.</summary>
    [HttpGet("lookup/{reference}")]
    [AllowAnonymous]
    [ProducesResponseType<ComplaintLookupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Lookup(string reference, CancellationToken ct = default)
        => Ok(ApiResponse<ComplaintLookupDto>.Ok(await _sender.Send(new LookupComplaintQuery(reference), ct)));

    /// <summary>Khiếu nại của chính user đang đăng nhập.</summary>
    [HttpGet("my")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ComplaintDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMy(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyComplaintsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ComplaintDto>>.Ok(result));
    }

    /// <summary>Admin — hàng đợi khiếu nại chưa xử lý xong (Open/Investigating), mới nhất trước.</summary>
    [HttpGet("pending")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ComplaintDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingComplaintsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ComplaintDto>>.Ok(result));
    }

    /// <summary>Admin — xử lý khiếu nại (Investigating/Resolved/Rejected), kèm hành động xử lý nếu
    /// Resolved (Refund/IssueWarning/Dismiss/TakeDownContent).
    /// <para>Refund hoàn tiền vé của RIÊNG người khiếu nại, show vẫn diễn; TakeDownContent hủy hẳn
    /// show và hoàn 100% cho MỌI người giữ vé; IssueWarning tạo án phạt cho venue; Dismiss là bác
    /// khiếu nại. Mỗi hành động đều có hậu quả thật — không có hành động nào chỉ lưu nhãn.</para></summary>
    [HttpPost("{id:int}/resolve")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        int id, [FromBody] ResolveComplaintRequest body, CancellationToken ct = default)
    {
        await _sender.Send(
            new ResolveComplaintCommand(id, body.Status, body.Resolution, body.ResolvedAction), ct);
        return NoContent();
    }
}

public sealed record ResolveComplaintRequest(string Status, string? Resolution, string? ResolvedAction);
