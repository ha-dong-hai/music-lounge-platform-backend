using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.Commands.DeactivateMyAccount;
using MusicLounge.Application.Users.Commands.RequestDataErasure;
using MusicLounge.Application.Users.Commands.RequestPhoneVerification;
using MusicLounge.Application.Users.Commands.SubmitCitizenCard;
using MusicLounge.Application.Users.Commands.UpdateAiPreferences;
using MusicLounge.Application.Users.Commands.UpdateMyProfile;
using MusicLounge.Application.Users.Commands.VerifyPhone;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Application.Users.Queries.GetMyCitizenCardImage;
using MusicLounge.Application.Users.Queries.GetMyDataExport;
using MusicLounge.Application.Users.Queries.GetMyEarnings;
using MusicLounge.Application.Users.Queries.GetMyProfile;
using MusicLounge.Application.Users.Queries.GetOwnerTransactionHistory;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class MeController : ControllerBase
{
    private readonly ISender _sender;

    public MeController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<UserProfileDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyProfileQuery(), ct);
        return Ok(ApiResponse<UserProfileDto>.Ok(result));
    }

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

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateMyProfileCommand command,
        CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>KYC — nộp số CCCD/CMND + ảnh mặt trước/sau (lấy URL từ POST /uploads/images trước đó).</summary>
    [HttpPost("citizen-card")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitCitizenCard(
        [FromBody] SubmitCitizenCardCommand command,
        CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Xem lại ảnh CCCD/CMND đã nộp — chỉ chính chủ. File nằm ngoài wwwroot, không đoán URL truy cập trực tiếp được.</summary>
    [HttpGet("citizen-card/{side}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyCitizenCardImage(string side, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyCitizenCardImageQuery(side), ct);
        return File(result.Content, result.ContentType);
    }

    /// <summary>91/2025/QH15 (DSAR): xuất toàn bộ dữ liệu cá nhân đang lưu — đáp ứng nghĩa vụ ACK 2 ngày ngay lập tức (đồng bộ).</summary>
    [HttpGet("data-export")]
    [ProducesResponseType<ApiResponse<MyDataExportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyDataExport(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyDataExportQuery(), ct);
        return Ok(ApiResponse<MyDataExportDto>.Ok(result));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateMyAccount(CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateMyAccountCommand(), ct);
        return NoContent();
    }

    /// <summary>
    /// 91/2025/QH15 (DSAR): xóa dữ liệu cá nhân — khác DELETE /me (tạm khóa, còn khôi phục được):
    /// đây là hành động không thể hoàn tác, xóa hẳn thông tin định danh (email/tên/SĐT/CCCD...).
    /// Lịch sử vé/donate/thanh toán vẫn được giữ (Luật Kế toán yêu cầu lưu 10 năm) nhưng không còn
    /// gắn với danh tính thật của bạn nữa. Tài khoản đăng nhập bằng mật khẩu cần xác nhận lại mật
    /// khẩu hiện tại; tài khoản Google không cần.
    /// </summary>
    [HttpPost("data-erasure")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestDataErasure(
        [FromBody] RequestDataErasureRequest? body, CancellationToken ct = default)
    {
        await _sender.Send(new RequestDataErasureCommand(body?.CurrentPassword), ct);
        return NoContent();
    }

    /// <summary>NĐ 147/2024: gửi mã OTP 6 số về số điện thoại đã lưu trong hồ sơ để xác thực.</summary>
    [HttpPost("phone/verification-code")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestPhoneVerification(CancellationToken ct = default)
    {
        await _sender.Send(new RequestPhoneVerificationCommand(), ct);
        return NoContent();
    }

    [HttpPost("phone/verify")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyPhone(
        [FromBody] VerifyPhoneCommand command, CancellationToken ct = default)
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

/// <summary>CurrentPassword required for local accounts, ignored for Google-only accounts.</summary>
public sealed record RequestDataErasureRequest(string? CurrentPassword);
