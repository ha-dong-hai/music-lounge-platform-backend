using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Analytics.Queries.GetOwnerRevenueReport;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analytics")]
[Authorize(Policy = Policies.RequireOwner)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ISender _sender;

    public AnalyticsController(ISender sender) => _sender = sender;

    /// <summary>Báo cáo doanh thu tổng hợp: vé + F&B + donate, tổng hợp theo sự kiện và theo
    /// tháng, lọc theo khoảng thời gian tuỳ chọn (mặc định toàn bộ lịch sử).</summary>
    [HttpGet("revenue-report")]
    [ProducesResponseType<ApiResponse<OwnerRevenueReportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] int loungeId,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerRevenueReportQuery(loungeId, from, to), ct);
        return Ok(ApiResponse<OwnerRevenueReportDto>.Ok(result));
    }
}
