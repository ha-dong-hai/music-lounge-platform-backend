using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.Commands.ResolveContentReport;
using MusicLounge.Application.Moderations.Commands.SubmitContentReport;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Application.Moderations.Queries.GetContentReportQueue;

namespace MusicLounge.Api.Controllers;

// MLACP-222: hang doi bao cao vi pham cho noi dung DA hien thi (show/livestream/rating) — khac voi
// ModerationController (cong duyet AI truoc khi dang).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/content-reports")]
public sealed class ContentReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ContentReportsController(ISender sender) => _sender = sender;

    /// <summary>Người dùng đã đăng nhập — báo cáo 1 nội dung (show/livestream/rating) vi phạm.
    /// 409 nếu bạn đã báo cáo nội dung này và báo cáo đó vẫn đang chờ xử lý.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitContentReportCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(id));
    }

    /// <summary>Admin — hàng đợi nội dung bị báo cáo, sắp xếp theo số lần báo cáo giảm dần (nội
    /// dung bị báo cáo nhiều nhất lên đầu), kèm hạn SLA gỡ bỏ (mặc định 48h theo NĐ 147/2024).</summary>
    [HttpGet("queue")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ContentReportQueueItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetContentReportQueueQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ContentReportQueueItemDto>>.Ok(result));
    }

    /// <summary>Admin — xử lý toàn bộ báo cáo đang mở cho 1 nội dung: Removed (gỡ nội dung, có hiệu
    /// lực ngay) hoặc Dismissed (bỏ qua báo cáo, giữ nguyên nội dung).</summary>
    [HttpPost("resolve")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(
        [FromBody] ResolveContentReportCommand command, CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }
}
