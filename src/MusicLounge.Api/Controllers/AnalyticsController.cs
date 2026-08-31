using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Analytics.Queries.GetAdminPlatformOverview;
using MusicLounge.Application.Analytics.Queries.GetOwnerRevenueReport;
using MusicLounge.Application.Analytics.Queries.GetShowPerformance;
using MusicLounge.Application.Analytics.Queries.GetTicketSalesTrend;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analytics")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ISender _sender;

    public AnalyticsController(ISender sender) => _sender = sender;

    /// <summary>Báo cáo doanh thu tổng hợp: vé + F&B + donate, tổng hợp theo sự kiện và theo
    /// tháng, lọc theo khoảng thời gian tuỳ chọn (mặc định toàn bộ lịch sử).</summary>
    [HttpGet("revenue-report")]
    [Authorize(Policy = Policies.RequireOwner)]
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

    /// <summary>Admin — tổng quan nền tảng: số phòng trà đang hoạt động (hiện tại, không theo kỳ),
    /// tổng sự kiện/doanh thu nền tảng/khán giả đăng ký mới trong kỳ (mặc định tháng hiện tại,
    /// giờ VN). Tuỳ chọn from/to để xem kỳ khác.</summary>
    [HttpGet("admin-overview")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<AdminPlatformOverviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminOverview(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetAdminPlatformOverviewQuery(from, to), ct);
        return Ok(ApiResponse<AdminPlatformOverviewDto>.Ok(result));
    }

    /// <summary>Owner — thống kê hiệu suất 1 sự kiện: lượt xem trang, tỷ lệ chuyển đổi sang mua vé,
    /// check-in thực tế so với vé bán, số người xem live.</summary>
    [HttpGet("shows/{showId:int}/performance")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<ShowPerformanceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShowPerformance(int showId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowPerformanceQuery(showId), ct);
        return Ok(ApiResponse<ShowPerformanceDto>.Ok(result));
    }

    /// <summary>Owner — biểu đồ bán vé theo ngày trong thời gian mở bán của 1 sự kiện, cùng tỷ lệ
    /// bán theo từng loại vé.</summary>
    [HttpGet("shows/{showId:int}/ticket-sales-trend")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<TicketSalesTrendDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketSalesTrend(int showId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTicketSalesTrendQuery(showId), ct);
        return Ok(ApiResponse<TicketSalesTrendDto>.Ok(result));
    }
}
