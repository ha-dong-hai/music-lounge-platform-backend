using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.Commands.AddPerformance;
using MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.ChangeLoungeShowFormat;
using MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.DeleteLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.DeletePerformance;
using MusicLounge.Application.LoungeShows.Commands.EndLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.PublishLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.RateShow;
using MusicLounge.Application.LoungeShows.Commands.RescheduleLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.StartLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.UpdatePerformance;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowSuggestions;
using MusicLounge.Application.LoungeShows.Queries.GetMyLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.GetShowRatings;
using MusicLounge.Application.LoungeShows.Queries.GetSimilarLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.GetTrendingLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Queries.GetShowTicketStats;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (duyet/tu choi cua Admin, doi trang thai khac...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lounge-shows")]
public sealed class LoungeShowsController : ControllerBase
{
    private readonly ISender _sender;

    public LoungeShowsController(ISender sender) => _sender = sender;

    /// <summary>Tìm kiếm sự kiện công khai — kết hợp được nhiều bộ lọc cùng lúc: thể loại
    /// nhạc/dòng nhạc/không gian, hình thức tổ chức, khoảng thời gian diễn ra, từ khóa trong tên/mô
    /// tả. Chỉ trả sự kiện đã duyệt công khai (Published/Ongoing) và sắp diễn. Kết quả phân trang,
    /// mỗi sự kiện kèm giá thấp nhất/cao nhất và thông tin phòng trà (đã có sẵn trong
    /// LoungeShowListItemDto).</summary>
    [HttpGet("search")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] int[]? genreIds,
        [FromQuery] int[]? moodIds,
        [FromQuery] int[]? atmosphereIds,
        [FromQuery] string? keyword,
        [FromQuery] LoungeShowFormat? format,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] LoungeShowSortBy sortBy = LoungeShowSortBy.Newest,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new SearchLoungeShowsQuery(
            genreIds, moodIds, atmosphereIds, keyword, format, dateFrom, dateTo,
            page, pageSize, sortBy), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpGet("trending")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrending(
        [FromQuery] int limit = 10,
        [FromQuery] string? city = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTrendingLoungeShowsQuery(limit, city), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpGet("suggestions")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] string q,
        [FromQuery] int limit = 8,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>.Ok([]));

        var result = await _sender.Send(new GetLoungeShowSuggestionsQuery(q, limit), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>.Ok(result));
    }

    /// <summary>Chỉ Owner của đúng phòng trà được chọn mới tạo được (403 nếu khác). Cần có gói
    /// subscription đang hoạt động tại thời điểm tạo (không phải lúc publish). Sự kiện mới luôn ở
    /// trạng thái nháp (LoungeShowStatus.Draft).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLoungeShowCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }

    /// <summary>Chỉ trả sự kiện của đúng Owner đang đăng nhập (mọi trạng thái, kể cả Draft) — lọc
    /// theo trạng thái qua query param `status` nếu có.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromQuery] LoungeShowStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyLoungeShowsQuery(status, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    /// <summary>Sự kiện đang Draft chỉ Owner/Staff của đúng venue (hoặc Admin) xem được — người
    /// khác nhận 404 (không lộ sự tồn tại của bản nháp). Sự kiện đã publish thì công khai.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<LoungeShowDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeShowDetailQuery(id), ct);
        return Ok(ApiResponse<LoungeShowDetailDto>.Ok(result));
    }

    /// <summary>Tối đa 6 sự kiện "tương tự" cho trang chi tiết — cùng phòng trà HOẶC chung ít nhất 1
    /// thể loại nhạc với sự kiện đang xem, luôn loại trừ chính sự kiện đó, chỉ show Published/
    /// Ongoing. Ưu tiên show khớp cả 2 tiêu chí trước, còn lại theo ngày diễn gần nhất.</summary>
    [HttpGet("{id:int}/similar")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilar(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetSimilarLoungeShowsQuery(id), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeShowListItemDto>>.Ok(result));
    }

    /// <summary>Thống kê vé đã bán của sự kiện cho Owner: tổng vé, doanh thu, số vé đã check-in,
    /// và breakdown theo từng mức giá — đếm trực tiếp trên bảng Ticket tại thời điểm gọi (không
    /// dùng field đếm sẵn nào) nên luôn phản ánh đúng thời điểm hiện tại. Chỉ Owner của venue (hoặc
    /// Admin) xem được (403 nếu khác).</summary>
    [HttpGet("{id:int}/ticket-stats")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<ShowTicketStatsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketStats(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowTicketStatsQuery(id), ct);
        return Ok(ApiResponse<ShowTicketStatsDto>.Ok(result));
    }

    /// <summary>Chỉ sửa được khi sự kiện còn ở trạng thái Draft (422 nếu đã gửi duyệt/đã đăng);
    /// chỉ đúng Owner sở hữu venue mới sửa được (403 nếu khác).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateLoungeShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateLoungeShowCommand(
            id, body.Name, body.Description, body.ScheduledStart, body.ScheduledEnd,
            body.CategoryId, body.OfflineQuota, body.OnlineQuota), ct);
        return NoContent();
    }

    /// <summary>Xóa thật (hard delete) — chỉ áp dụng cho sự kiện còn ở trạng thái Draft (422 nếu
    /// khác); sự kiện đã publish/đang diễn ra phải dùng huỷ (Cancel), không xóa được nữa.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteLoungeShowCommand(id), ct);
        return NoContent();
    }

    /// <summary>Gửi duyệt: chuyển Draft → Pending, tạo bản ghi kiểm duyệt cho Admin. Bắt buộc đã có
    /// ≥1 hạng vé, ≥1 nghệ sĩ trong line-up, văn bản chấp thuận biểu diễn (NĐ 144/2020 Điều 10), và
    /// nộp trước tối thiểu N ngày làm việc so với ngày diễn — thiếu bất kỳ điều kiện nào trả về lỗi
    /// nêu rõ (422).</summary>
    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(int id, CancellationToken ct = default)
    {
        await _sender.Send(new PublishLoungeShowCommand(id), ct);
        return NoContent();
    }

    /// <summary>Hủy sự kiện đã đăng — vé đã Confirmed được hủy kèm tạo yêu cầu hoàn 100% tiền
    /// (RefundRequest) và thông báo tới từng người mua.</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct = default)
    {
        await _sender.Send(new CancelLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reschedule")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reschedule(
        int id, [FromBody] RescheduleLoungeShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RescheduleLoungeShowCommand(id, body.NewScheduledStart), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(int id, CancellationToken ct = default)
    {
        await _sender.Send(new StartLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/end")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> End(int id, CancellationToken ct = default)
    {
        await _sender.Send(new EndLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/format")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeFormat(
        int id, [FromBody] ChangeLoungeShowFormatRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ChangeLoungeShowFormatCommand(id, body.NewFormat), ct);
        return NoContent();
    }

    /// <summary>Thêm nghệ sĩ vào danh sách biểu diễn — chỉ khi sự kiện còn Draft (422 nếu khác).
    /// Trả 409 nếu nghệ sĩ này đã có trong line-up của đúng sự kiện này.</summary>
    [HttpPost("{id:int}/performances")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddPerformance(
        int id, [FromBody] AddPerformanceRequest body, CancellationToken ct = default)
    {
        var performanceId = await _sender.Send(new AddPerformanceCommand(
            id, body.PerformerId, body.PerformerName, body.Role,
            body.OrderIndex, body.SetTime, body.AcceptsDonation), ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1.0" }, ApiResponse<int>.Ok(performanceId));
    }

    /// <summary>Sửa vai trò/thứ tự/giờ diễn/bật-tắt nhận donate của 1 nghệ sĩ trong line-up — chỉ
    /// khi sự kiện còn Draft (422 nếu khác). Đổi sang nghệ sĩ khác: xóa rồi thêm lại.</summary>
    [HttpPut("{id:int}/performances/{performanceId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePerformance(
        int id, int performanceId, [FromBody] UpdatePerformanceRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdatePerformanceCommand(
            performanceId, body.Role, body.OrderIndex, body.SetTime, body.AcceptsDonation), ct);
        return NoContent();
    }

    /// <summary>Xóa 1 nghệ sĩ khỏi danh sách biểu diễn — chỉ khi sự kiện còn Draft (422 nếu khác).</summary>
    [HttpDelete("{id:int}/performances/{performanceId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeletePerformance(int id, int performanceId, CancellationToken ct = default)
    {
        await _sender.Send(new DeletePerformanceCommand(performanceId), ct);
        return NoContent();
    }

    /// <summary>Chấm sao 1-5 + nhận xét sau khi show kết thúc — chỉ mở trong 7 ngày kể từ lúc kết
    /// thúc (§6.13). Bắt buộc đã "check-in" thật (vé vào cửa đã quét QR, hoặc vé xem livestream đã
    /// từng thực sự nhận được URL phát — xem RateShowCommandHandler), không chỉ cần có vé Confirmed.
    /// Mỗi người chỉ đánh giá được 1 lần cho 1 show (409 nếu đã đánh giá).</summary>
    [HttpPost("{id:int}/rate")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rate(
        int id, [FromBody] RateShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RateShowCommand(id, body.Score, body.Comment), ct);
        return NoContent();
    }

    /// <summary>Danh sách đánh giá công khai của 1 sự kiện — điểm trung bình + phân bố sao (1-5)
    /// tính trên toàn bộ đánh giá còn hiệu lực, danh sách nhận xét phân trang sắp mới nhất lên
    /// trước. Đánh giá đã bị Admin gỡ (IsRemoved) không tính vào điểm trung bình/phân bố và không
    /// xuất hiện trong danh sách.</summary>
    [HttpGet("{id:int}/ratings")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<ShowRatingsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRatings(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowRatingsQuery(id, page, pageSize), ct);
        return Ok(ApiResponse<ShowRatingsDto>.Ok(result));
    }
}

public sealed record RescheduleLoungeShowRequest(DateTimeOffset NewScheduledStart);
public sealed record ChangeLoungeShowFormatRequest(LoungeShowFormat NewFormat);

public sealed record UpdateLoungeShowRequest(
    string Name,
    string Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    int? CategoryId,
    int? OfflineQuota,
    int? OnlineQuota);

public sealed record AddPerformanceRequest(
    int? PerformerId,
    string? PerformerName,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation);

public sealed record UpdatePerformanceRequest(
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation);

public sealed record RateShowRequest(int Score, string? Comment);
