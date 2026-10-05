using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.ShowCancellationReviews;

namespace MusicLounge.Api.Controllers;

/// <summary>MLACP-676 — Admin xét lý do chủ phòng trà huỷ buổi hòa nhạc đã mở bán: miễn phạt hay phạt.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/show-cancellations")]
[Authorize(Policy = Policies.RequireAdmin)]
public sealed class ShowCancellationReviewsController : ControllerBase
{
    private readonly ISender _sender;

    public ShowCancellationReviewsController(ISender sender) => _sender = sender;

    /// <summary>Danh sách lý do huỷ — mặc định đang chờ xét (hạn gần nhất lên trước). status: Pending | Excused | Penalized | All.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<ShowCancellationReviewDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] string? status = "Pending", [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowCancellationReviewsQuery(status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ShowCancellationReviewDto>>.Ok(result));
    }

    /// <summary>Xét một lý do huỷ: Excuse (miễn phạt) hoặc Penalize (ra án phạt — mặc định cảnh cáo). 409 nếu đã xét.</summary>
    [HttpPost("{id:guid}/decide")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] DecideBody body, CancellationToken ct = default)
    {
        await _sender.Send(new DecideShowCancellationReviewCommand(id, body.Decision, body.Note, body.PenaltyType, body.SuspensionDays), ct);
        return NoContent();
    }

    public sealed record DecideBody(string Decision, string Note, string? PenaltyType = null, int? SuspensionDays = null);
}
