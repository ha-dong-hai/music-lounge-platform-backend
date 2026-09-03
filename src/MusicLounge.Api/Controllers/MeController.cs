using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.Commands.UpdateAiPreferences;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Application.Users.Queries.GetMyEarnings;
using MusicLounge.Application.Users.Queries.GetOwnerTransactionHistory;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (profile, KYC, DSAR, xac thuc SDT...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class MeController : ControllerBase
{
    private readonly ISender _sender;

    public MeController(ISender sender) => _sender = sender;

    /// <summary>Lưu sở thích âm nhạc (thể loại/tâm trạng/không gian) làm đầu vào cho AI gợi ý — dùng
    /// cả cho onboarding lần đầu sau đăng ký lẫn cập nhật lại trong Cài đặt sau này (ghi đè toàn bộ
    /// danh sách theo request, không phải thêm dần). Mọi field có thể để rỗng — không có bước nào
    /// khác trong hệ thống bắt buộc phải hoàn thành onboarding này mới dùng được ứng dụng.</summary>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateAiPreferencesCommand command,
        CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Lịch sử giao dịch hợp nhất của Owner (vé bán được, donate nhận, quyết toán đã nhận
    /// — cùng một nguồn sổ cái D8) — lọc theo khoảng thời gian và loại giao dịch
    /// (payment/donation/settlement).</summary>
    [HttpGet("transactions")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<OwnerTransactionDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyTransactionHistory(
        [FromQuery] string? type,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOwnerTransactionHistoryQuery(type, from, to, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<OwnerTransactionDto>>.Ok(result));
    }

    /// <summary>Tổng quan thu nhập của Owner từ settlement — đã nhận (Released), đang chờ
    /// (Scheduled/PendingReview), và 10 settlement gần nhất.</summary>
    [HttpGet("earnings")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<EarningsSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEarnings(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyEarningsQuery(), ct);
        return Ok(ApiResponse<EarningsSummaryDto>.Ok(result));
    }
}
