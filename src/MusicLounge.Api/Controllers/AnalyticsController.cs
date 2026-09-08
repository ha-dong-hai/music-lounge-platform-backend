using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Analytics.Queries.ExportOwnerRevenueReport;
using MusicLounge.Application.Analytics.Queries.GetAdminContentOverview;
using MusicLounge.Application.Analytics.Queries.GetAiRecommendationPerformance;
using MusicLounge.Application.Analytics.Queries.GetAdminPlatformOverview;
using MusicLounge.Application.Analytics.Queries.GetAudienceEngagementStats;
using MusicLounge.Application.Analytics.Queries.GetOwnerAnalytics;
using MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;
using MusicLounge.Application.Analytics.Queries.GetOwnerLivestreamHistory;
using MusicLounge.Application.Analytics.Queries.GetOwnerRevenueReport;
using MusicLounge.Application.Analytics.Queries.GetPlatformAnalytics;
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

    /// <summary>Owner — bảng tổng quan nhanh của 1 venue: tổng/số buổi diễn sắp tới/đã qua, vé bán
    /// (offline/online), doanh thu (vé+F&amp;B), đánh giá trung bình, donate chờ nghệ sĩ nhận, xu
    /// hướng doanh thu 6 tháng gần nhất, top 5 show doanh thu cao nhất — mọi thời gian, không lọc
    /// theo kỳ (khác revenue-report/admin-overview vốn lọc theo kỳ, dùng khi cần báo cáo chi tiết
    /// hơn theo khoảng thời gian cụ thể).</summary>
    [HttpGet("my-lounge")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<OwnerAnalyticsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyLounge([FromQuery] int loungeId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerAnalyticsQuery(loungeId), ct);
        return Ok(ApiResponse<OwnerAnalyticsDto>.Ok(result));
    }

    /// <summary>Admin — bảng tổng quan nhanh toàn nền tảng: tổng venue, tổng show đã publish, tổng
    /// user, tổng vé đã bán, GMV, tổng donate, số moderation đang chờ — mọi thời gian (khác
    /// admin-overview vốn lọc theo kỳ).</summary>
    [HttpGet("platform")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<PlatformAnalyticsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlatform(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPlatformAnalyticsQuery(), ct);
        return Ok(ApiResponse<PlatformAnalyticsDto>.Ok(result));
    }

    /// <summary>Báo cáo doanh thu tổng hợp: vé + F&B + donate, tổng hợp theo buổi diễn và theo
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

    /// <summary>Owner — xuất báo cáo doanh thu ra file CSV (UTF-8 BOM, mở trực tiếp bằng Excel)
    /// để lưu trữ/nộp kế toán. Cùng số liệu với GET revenue-report (dùng chung 1 nguồn tính toán).</summary>
    [HttpGet("revenue-report/export")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportRevenueReport(
        [FromQuery] int loungeId,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var file = await _sender.Send(new ExportOwnerRevenueReportQuery(loungeId, from, to), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Admin — tổng quan nền tảng: số phòng trà đang hoạt động (hiện tại, không theo kỳ),
    /// tổng buổi diễn/doanh thu nền tảng/khán giả đăng ký mới trong kỳ (mặc định tháng hiện tại,
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

    /// <summary>Owner — thống kê hiệu suất 1 buổi diễn: lượt xem trang, tỷ lệ chuyển đổi sang mua vé,
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

    /// <summary>Owner — biểu đồ bán vé theo ngày trong thời gian mở bán của 1 buổi diễn, cùng tỷ lệ
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

    /// <summary>Owner — tổng donate theo từng nghệ sĩ, tổng hợp qua toàn bộ buổi diễn của venue,
    /// kèm nghệ sĩ được donate nhiều nhất.</summary>
    [HttpGet("artist-donations")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<OwnerArtistDonationReportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtistDonationStats(
        [FromQuery] int loungeId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerArtistDonationStatsQuery(loungeId), ct);
        return Ok(ApiResponse<OwnerArtistDonationReportDto>.Ok(result));
    }

    /// <summary>Admin — thống kê nội dung &amp; giám sát: số buổi diễn chờ duyệt, số khiếu nại chưa
    /// xử lý, số vi phạm phát sinh trong tháng hiện tại (giờ VN), và bảng xếp hạng phòng trà theo
    /// điểm uy tín (ReputationScore).</summary>
    [HttpGet("admin-content-overview")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<AdminContentOverviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminContentOverview(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetAdminContentOverviewQuery(), ct);
        return Ok(ApiResponse<AdminContentOverviewDto>.Ok(result));
    }

    /// <summary>Owner — lịch sử các phiên livestream đã kết thúc (Ended/Terminated/Failed) của
    /// venue, mới nhất trước: peak viewer, tổng lượt xem, doanh thu PPV, tổng donate trong phiên.</summary>
    [HttpGet("livestream-history")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<LivestreamHistoryItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLivestreamHistory(
        [FromQuery] int loungeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerLivestreamHistoryQuery(loungeId, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<LivestreamHistoryItemDto>>.Ok(result));
    }

    /// <summary>Admin — thống kê tương tác khán giả: số follow mới, số wishlist mới, số đánh giá
    /// mới trong kỳ (mặc định tháng hiện tại, giờ VN), và tỷ lệ khán giả mua vé ≥2 buổi diễn khác
    /// nhau trong cùng kỳ ("tỷ lệ quay lại").</summary>
    [HttpGet("audience-engagement")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<AudienceEngagementStatsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAudienceEngagementStats(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetAudienceEngagementStatsQuery(from, to), ct);
        return Ok(ApiResponse<AudienceEngagementStatsDto>.Ok(result));
    }

    /// <summary>Admin — hiệu suất AI gợi ý trong kỳ (mặc định tháng hiện tại, giờ VN): tỷ lệ khán
    /// giả xem/bấm vào buổi diễn được gợi ý (click-through), tỷ lệ mua vé sau khi được gợi ý
    /// (conversion) — tính theo từng cặp (user, buổi diễn) duy nhất từng được gợi ý trong kỳ.</summary>
    [HttpGet("ai-recommendation-performance")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<AiRecommendationPerformanceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAiRecommendationPerformance(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetAiRecommendationPerformanceQuery(from, to), ct);
        return Ok(ApiResponse<AiRecommendationPerformanceDto>.Ok(result));
    }
}
