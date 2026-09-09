using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Catalog.Commands.CreateEventCategory;
using MusicLounge.Application.Catalog.Commands.CreateMood;
using MusicLounge.Application.Catalog.Commands.CreateMusicGenre;
using MusicLounge.Application.Catalog.Commands.CreateVenueAtmosphere;
using MusicLounge.Application.Catalog.Commands.DeleteEventCategory;
using MusicLounge.Application.Catalog.Commands.DeleteMood;
using MusicLounge.Application.Catalog.Commands.DeleteMusicGenre;
using MusicLounge.Application.Catalog.Commands.DeleteVenueAtmosphere;
using MusicLounge.Application.Catalog.Commands.UpdateEventCategory;
using MusicLounge.Application.Catalog.Commands.UpdateMood;
using MusicLounge.Application.Catalog.Commands.UpdateMusicGenre;
using MusicLounge.Application.Catalog.Commands.UpdateVenueAtmosphere;
using MusicLounge.Application.Admin.Commands.UpdateSystemConfig;
using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Admin.Queries.GetSystemConfigHistory;
using MusicLounge.Application.Admin.Queries.GetSystemConfigs;
using MusicLounge.Application.Admin.Queries.GetLedgerIntegrity;
using MusicLounge.Application.Admin.Commands.ReviewVenue;
using MusicLounge.Application.Admin.Queries.GetVenueReviewQueue;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Application.LoungeShows.Commands.RemoveRating;
using MusicLounge.Application.Moderations.Commands.ReviewShow;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Application.Moderations.Queries.GetPendingLoungeShows;
using MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Application.Refunds.Queries.GetPendingRefundRequests;
using MusicLounge.Application.Users.Commands.DeactivateUserAccount;
using MusicLounge.Application.Users.Commands.ReactivateUserAccount;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Admin.Commands.ReviewKycDocument;
using MusicLounge.Application.Admin.Commands.TriggerRecurringJob;
using MusicLounge.Application.Admin.Queries.GetKycReviewQueue;
using MusicLounge.Application.Users.Queries.GetCitizenCardImage;
using MusicLounge.Application.Users.Queries.GetUserDetail;
using MusicLounge.Application.Users.Queries.GetUsers;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Api.Controllers;

/// <summary>Admin quản lý 4 danh mục dùng chung toàn nền tảng (thể loại nhạc, dòng nhạc/cảm xúc,
/// phong cách không gian, loại buổi diễn). Xóa bị chặn (409) nếu danh mục còn đang được show/nghệ
/// sĩ/người dùng nào tham chiếu — buộc gỡ liên kết trước, không âm thầm để lại dữ liệu mồ côi.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = Policies.RequireAdmin)]
public sealed class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    // ---- Sổ cái ----

    /// <summary>Rà soát tính toàn vẹn sổ cái kép: bút toán mất cân bằng (tổng nợ ≠ tổng có trong 1
    /// journal) và callback VNPay bị xử lý trùng (2 journal riêng biệt cho cùng 1 lần xác nhận
    /// thanh toán) — trả về rỗng nếu sổ cái cân bằng hoàn toàn.</summary>
    [HttpGet("ledger/integrity-check")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LedgerIntegrityIssueDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LedgerIntegrityCheck(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLedgerIntegrityQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<LedgerIntegrityIssueDto>>.Ok(result));
    }

    // ---- Thể loại nhạc ----

    [HttpPost("genres")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGenre(
        [FromBody] CreateMusicGenreCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }

    [HttpPut("genres/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateGenre(
        int id, [FromBody] UpdateMusicGenreRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateMusicGenreCommand(id, body.Name, body.NameEn), ct);
        return NoContent();
    }

    [HttpDelete("genres/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteGenre(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteMusicGenreCommand(id), ct);
        return NoContent();
    }

    // ---- Dòng nhạc/cảm xúc ----

    [HttpPost("moods")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMood(
        [FromBody] CreateMoodCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }

    [HttpPut("moods/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMood(
        int id, [FromBody] UpdateMoodRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateMoodCommand(id, body.Name), ct);
        return NoContent();
    }

    [HttpDelete("moods/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteMood(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteMoodCommand(id), ct);
        return NoContent();
    }

    // ---- Phong cách không gian ----

    [HttpPost("atmospheres")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAtmosphere(
        [FromBody] CreateVenueAtmosphereCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }

    [HttpPut("atmospheres/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAtmosphere(
        int id, [FromBody] UpdateVenueAtmosphereRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateVenueAtmosphereCommand(id, body.Name), ct);
        return NoContent();
    }

    [HttpDelete("atmospheres/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAtmosphere(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteVenueAtmosphereCommand(id), ct);
        return NoContent();
    }

    // ---- Loại buổi diễn ----

    [HttpPost("event-categories")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEventCategory(
        [FromBody] CreateEventCategoryCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }

    [HttpPut("event-categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEventCategory(
        int id, [FromBody] UpdateEventCategoryRequest body, CancellationToken ct = default)
    {
        await _sender.Send(
            new UpdateEventCategoryCommand(id, body.Name, body.Description, body.IsActive), ct);
        return NoContent();
    }

    [HttpDelete("event-categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEventCategory(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteEventCategoryCommand(id), ct);
        return NoContent();
    }

    // ---- Duyệt buổi diễn ----

    /// <summary>Danh sách buổi diễn đang chờ duyệt (Pending), kèm tên/phòng trà/ngày diễn và tín hiệu
    /// AI moderation (điểm rủi ro, lý do gắn cờ) để Admin ưu tiên xử lý — sắp xếp theo điểm rủi ro
    /// AI giảm dần. Xem chi tiết đầy đủ 1 event: dùng GET /lounge-shows/{id} (Admin xem được cả
    /// Draft/Pending).</summary>
    [HttpGet("shows/pending")]
    [ProducesResponseType<ApiResponse<PaginatedResult<PendingLoungeShowDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingShows(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingLoungeShowsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<PendingLoungeShowDto>>.Ok(result));
    }

    /// <summary>Duyệt (Approved → Published) hoặc từ chối (Rejected → về lại Draft để Owner sửa và
    /// nộp lại) một event đang chờ duyệt (Pending). Chỉ xử lý được 1 lần — duyệt lại event đã có
    /// quyết định trả về 409.</summary>
    [HttpPost("shows/{id:int}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewShow(
        int id, [FromBody] ReviewShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ReviewShowCommand(id, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    // ---- Duyệt hồ sơ phòng trà ----

    /// <summary>Hàng đợi hồ sơ phòng trà chờ duyệt, cũ nhất trước. Cờ hasBusinessLicense cho biết
    /// hồ sơ đã có giấy phép kinh doanh để xét hay chưa — trường đó không bắt buộc lúc tạo, nên có
    /// hồ sơ nộp lên mà không kèm căn cứ nào. Lọc status=Rejected để xem lại các hồ sơ đã từ
    /// chối.</summary>
    [HttpGet("venues/pending")]
    [ProducesResponseType<ApiResponse<PaginatedResult<VenueReviewItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVenueReviewQueue(
        [FromQuery] LoungeStatus status = LoungeStatus.Pending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetVenueReviewQueueQuery(status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<VenueReviewItemDto>>.Ok(result));
    }

    /// <summary>Duyệt hoặc từ chối hồ sơ một phòng trà (BR-01). Từ chối bắt buộc nêu lý do, và Owner
    /// được thông báo kết quả. Chỉ xử lý hồ sơ đang ở Pending hoặc Rejected: Suspended/Locked/Warned
    /// là trạng thái do án phạt quản, gỡ chúng ở đây sẽ thành đường vòng bỏ qua luồng khiếu nại.
    /// Đây là bước quyết định một địa điểm có được bán vé hay không — trước MLACP-307 không có bước
    /// này, nên phòng trà chưa ai xác minh vẫn thu tiền vé thật.</summary>
    [HttpPost("venues/{id:int}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReviewVenue(
        int id, [FromBody] ReviewVenueRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ReviewVenueCommand(id, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    // ---- Hoàn tiền ----

    /// <summary>Danh sách các yêu cầu hoàn tiền đang chờ xử lý (Pending), mới nhất trước.</summary>
    [HttpGet("refund-requests")]
    [ProducesResponseType<ApiResponse<PaginatedResult<RefundRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRefundRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingRefundRequestsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<RefundRequestDto>>.Ok(result));
    }

    /// <summary>Duyệt hoặc từ chối 1 yêu cầu hoàn tiền (Pending). Approved: gọi VNPay Merchant API
    /// (vnp_Command=refund) hoàn tiền thật trước, chỉ ghi đảo bút toán sổ cái (D8) và co giãn
    /// settlement tranche chưa release nếu VNPay xác nhận thành công; đánh dấu Payment.Refunded nếu
    /// tổng đã hoàn = GrossAmount. Chỉ xử lý được 1 lần (409 nếu đã xử lý). LƯU Ý: VNPay mặc định
    /// khóa chức năng hoàn tiền trên tài khoản sandbox — cần liên hệ VNPay để mở trước khi gọi được
    /// thành công, không phụ thuộc vào code đúng hay sai (503 nếu VNPay từ chối/lỗi).</summary>
    [HttpPost("refund-requests/{id:int}/process")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ProcessRefundRequest(
        int id, [FromBody] ProcessRefundRequestBody body, CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        await _sender.Send(new ProcessRefundRequestCommand(id, body.Decision, body.ApprovedAmount, ip), ct);
        return NoContent();
    }

    // ---- Đánh giá ----

    /// <summary>Gỡ 1 đánh giá vi phạm nội quy — không xoá cứng, chỉ đánh dấu IsRemoved kèm lý do nên
    /// vẫn còn trong hệ thống để đối soát, nhưng bị GetShowRatingsQueryHandler lọc khỏi trang sự
    /// kiện công khai. Chỉ xử lý được 1 lần (409 nếu đã gỡ trước đó).</summary>
    [HttpPost("ratings/{id:int}/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveRating(
        int id, [FromBody] RemoveRatingRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RemoveRatingCommand(id, body.Reason), ct);
        return NoContent();
    }

    // ---- Người dùng ----

    [HttpGet("users")]
    [ProducesResponseType<ApiResponse<PaginatedResult<UserAdminDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? searchText,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetUsersQuery(searchText, role, isActive, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<UserAdminDto>>.Ok(result));
    }

    [HttpGet("users/{id:int}")]
    [ProducesResponseType<ApiResponse<UserAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetUserDetailQuery(id), ct);
        return Ok(ApiResponse<UserAdminDto>.Ok(result));
    }

    /// <summary>Admin xem ảnh CCCD/CMND của user để xác thực danh tính — file nằm ngoài wwwroot, không đoán URL được.</summary>
    [HttpGet("users/{id:int}/citizen-card/{side}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserCitizenCardImage(int id, string side, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetCitizenCardImageQuery(id, side), ct);
        return File(result.Content, result.ContentType);
    }

    /// <summary>Các job định kỳ đang được đăng ký. Đọc từ chính chỗ đăng ký lúc khởi động, nên
    /// danh sách này không thể lệch với thực tế.</summary>
    [HttpGet("jobs")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    public IActionResult GetRecurringJobs([FromServices] IBackgroundJobService jobs)
        => Ok(ApiResponse<IReadOnlyList<string>>.Ok(jobs.GetRecurringJobIds()));

    /// <summary>Chạy ngay một job định kỳ thay vì chờ tới lịch — dùng khi job lỡ nhịp, hoặc vừa sửa
    /// dữ liệu và muốn thấy kết quả ngay. Id sai bị chặn ở 400 thay vì im lặng không làm gì như
    /// hành vi mặc định của Hangfire. Có ghi log, vì vài job trong số này động vào tiền.</summary>
    [HttpPost("jobs/{jobId}/trigger")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerRecurringJob(string jobId, CancellationToken ct = default)
    {
        await _sender.Send(new TriggerRecurringJobCommand(jobId), ct);
        return NoContent();
    }

    /// <summary>Hàng đợi hồ sơ định danh/thuế đang chờ duyệt. Trước MLACP-290 hồ sơ nộp vào rồi nằm
    /// im: Admin xem được ảnh nhưng không có bước chấp nhận hay từ chối nào (phát hiện R7).</summary>
    [HttpGet("kyc-reviews")]
    [ProducesResponseType<ApiResponse<PaginatedResult<KycReviewItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKycReviewQueue(
        [FromQuery] KycReviewStatus status = KycReviewStatus.Pending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetKycReviewQueueQuery(status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<KycReviewItemDto>>.Ok(result));
    }

    /// <summary>Duyệt hoặc từ chối một hồ sơ. Từ chối bắt buộc nêu lý do, và người nộp được thông
    /// báo kết quả. Duyệt hồ sơ thuế của một doanh nghiệp là thứ dừng khấu trừ thuế cho họ — khai
    /// báo suông không làm được điều đó (NĐ 117/2025, xem MLACP-289).</summary>
    [HttpPost("kyc-reviews/{id:int}/{document}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReviewKycDocument(
        int id, KycDocument document, [FromBody] ReviewKycDocumentBody body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ReviewKycDocumentCommand(id, document, body.Approve, body.Note), ct);
        return NoContent();
    }

    [HttpPost("users/{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUserAccount(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateUserAccountCommand(id), ct);
        return NoContent();
    }

    [HttpPost("users/{id:int}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateUserAccount(int id, CancellationToken ct = default)
    {
        await _sender.Send(new ReactivateUserAccountCommand(id), ct);
        return NoContent();
    }

    // ---- Tham số nghiệp vụ ----

    /// <summary>Danh sách toàn bộ tham số nghiệp vụ đang áp dụng, kèm kiểu dữ liệu, mô tả, và ai
    /// đổi lần cuối. Cờ <c>isMoneyRate</c> đánh dấu những khoá là TỈ LỆ TIỀN — giao diện nên hiển
    /// thị chúng dưới dạng phần trăm và cảnh báo trước khi sửa, vì nhìn giá trị thô "0.05" thì
    /// không cách nào biết đó là toàn bộ hoa hồng của nền tảng.</summary>
    [HttpGet("system-config")]
    [ProducesResponseType<IReadOnlyList<SystemConfigDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemConfigs(CancellationToken ct = default)
        => Ok(new { success = true, data = await _sender.Send(new GetSystemConfigsQuery(), ct) });

    /// <summary>Toàn bộ lịch sử thay đổi của một tham số, mới nhất trước: giá trị cũ, giá trị mới,
    /// ai đổi, khi nào, và lý do. Bảng này là INSERT-only nên không sửa hay xoá được — đó chính là
    /// điều làm nó có giá trị khi đối soát.</summary>
    [HttpGet("system-config/{key}/history")]
    [ProducesResponseType<IReadOnlyList<SystemConfigHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemConfigHistory(string key, CancellationToken ct = default)
        => Ok(new { success = true, data = await _sender.Send(new GetSystemConfigHistoryQuery(key), ct) });

    /// <summary>Đổi giá trị một tham số nghiệp vụ. BẮT BUỘC ghi lý do — mỗi lần đổi sinh một dòng
    /// lịch sử bất biến lưu cả giá trị cũ lẫn mới. Tỉ lệ tiền bị chặn ngoài khoảng 0..1, và riêng
    /// hoa hồng + thuế bị chặn nếu tổng chạm 100% (chủ phòng trà sẽ nhận 0đ hoặc âm — sổ cái kép
    /// không biểu diễn được khoản chuyển âm). Các khoản đã cam kết không bị ảnh hưởng: donate chốt
    /// tỉ lệ chia lúc xác nhận, settlement chốt lúc tạo.</summary>
    [HttpPut("system-config/{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSystemConfig(
        string key, [FromBody] UpdateSystemConfigRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateSystemConfigCommand(key, body.ConfigValue, body.Note), ct);
        return NoContent();
    }
}

public sealed record UpdateMusicGenreRequest(string Name, string? NameEn);
public sealed record UpdateMoodRequest(string Name);
public sealed record UpdateVenueAtmosphereRequest(string Name);
public sealed record UpdateEventCategoryRequest(string Name, string? Description, bool IsActive);
public sealed record ReviewShowRequest(string Decision, string? ReviewNote);

public sealed record ReviewVenueRequest(string Decision, string? ReviewNote);
public sealed record ProcessRefundRequestBody(string Decision, decimal? ApprovedAmount);
public sealed record RemoveRatingRequest(string Reason);
public sealed record UpdateSystemConfigRequest(string ConfigValue, string Note);
public sealed record ReviewKycDocumentBody(bool Approve, string? Note);
