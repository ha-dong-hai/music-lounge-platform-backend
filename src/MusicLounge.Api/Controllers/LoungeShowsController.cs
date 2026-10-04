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
using MusicLounge.Application.LoungeShows.Commands.GeneratePoster;
using MusicLounge.Application.LoungeShows.Commands.PublishLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.RateShow;
using MusicLounge.Application.LoungeShows.Commands.RescheduleLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;
using MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;
using MusicLounge.Application.LoungeShows.Commands.SetShowPoster;
using MusicLounge.Application.LoungeShows.Commands.SetVcpmcRoyaltyReference;
using MusicLounge.Application.LoungeShows.Commands.StartLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.UpdatePerformance;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.Queries.GetFilterOptions;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByLounge;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByPerformer;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowSuggestions;
using MusicLounge.Application.LoungeShows.Queries.GetMyLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.GetPosterGenerationHistory;
using MusicLounge.Application.LoungeShows.Queries.GetPublishedLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.GetShowRatings;
using MusicLounge.Application.LoungeShows.Queries.GetShowSeatingMap;
using MusicLounge.Application.LoungeShows.Queries.GetSimilarLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.GetTrendingLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Queries.GetShowOrders;
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

    /// <summary>Danh sách buổi diễn công khai (Published/Ongoing), hoặc buổi diễn của chính Owner đang
    /// gọi (mine=true, mọi trạng thái kể cả Draft) — khác /search ở chỗ không có bộ lọc, dùng cho
    /// trang chủ/"buổi diễn của tôi". Với mine=true (chủ phòng trà hoặc nhân viên — xem OperatedShows) lọc thêm được
    /// theo <c>status</c> và <c>format</c> (MLACP-498); hai tham số này gửi kèm mine=false thì trả 400.</summary>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublished(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] LoungeShowSortBy sortBy = LoungeShowSortBy.Newest,
        [FromQuery] bool includeSoldOut = true,
        [FromQuery] bool mine = false,
        [FromQuery] LoungeShowStatus? status = null,
        [FromQuery] LoungeShowFormat? format = null,
        [FromQuery] string? keyword = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetPublishedLoungeShowsQuery(page, pageSize, sortBy, includeSoldOut, mine, status, format, keyword), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    /// <summary>Tìm kiếm buổi diễn công khai — kết hợp được nhiều bộ lọc cùng lúc: thể loại
    /// nhạc/dòng nhạc/không gian, hình thức tổ chức, khoảng thời gian diễn ra, từ khóa trong tên/mô
    /// tả. Chỉ trả buổi diễn đã duyệt công khai (Published/Ongoing) và sắp diễn. Kết quả phân trang,
    /// mỗi buổi diễn kèm giá thấp nhất/cao nhất và thông tin phòng trà (đã có sẵn trong
    /// LoungeShowListItemDto).</summary>
    [HttpGet("search")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid[]? genreIds,
        [FromQuery] Guid[]? moodIds,
        [FromQuery] Guid[]? atmosphereIds,
        [FromQuery] string? keyword,
        [FromQuery] LoungeShowFormat? format,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] string? city = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] bool includeSoldOut = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] LoungeShowSortBy sortBy = LoungeShowSortBy.Newest,
        [FromQuery] string? provinceCode = null,
        [FromQuery] string? wardCode = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new SearchLoungeShowsQuery(
            genreIds, moodIds, atmosphereIds, keyword, format, dateFrom, dateTo,
            city, minPrice, maxPrice, includeSoldOut,
            page, pageSize, sortBy, provinceCode, wardCode), ct);
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
        // MLACP-519 (M-387 DEF-BE-06/07): `string q` không dấu ? với Nullable bật khiến ASP.NET Core coi q là BẮT BUỘC —
        // thiếu q hay ?q= rỗng bị 400 ngay ở model binding, nhánh "rỗng → danh sách rỗng" bên dưới không bao giờ chạy,
        // trong khi hợp đồng Swagger lại khai q tuỳ chọn. Ô gợi ý vừa xoá hết chữ phải nhận [] chứ không phải lỗi.
        [FromQuery] string? q = null,
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
    /// trạng thái nháp (LoungeShowStatus.Draft). Chính sách hoàn vé đặt tại đây và được công bố cho
    /// người mua ở GET /lounge-shows/{id} trước khi họ thanh toán; bỏ trống cả ba trường thì áp dụng
    /// mặc định của nền tảng (cho hủy, hoàn 100%, không hạn chót).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLoungeShowCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetDetail), new { id }, ApiResponse<Guid>.Ok(id));
    }

    /// <summary>Các buổi hòa nhạc của một phòng trà — công khai, cho trang giới thiệu venue mà
    /// khán giả xem trước khi mua vé. Khác `GET mine` bên dưới, vốn là danh sách riêng của chủ venue
    /// đang đăng nhập và gồm cả bản nháp.</summary>
    [HttpGet("by-lounge/{loungeId:guid}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLounge(
        Guid loungeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeShowsByLoungeQuery(loungeId, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    /// <summary>Trang nghệ sĩ: thông tin nghệ sĩ kèm các buổi hòa nhạc họ tham gia. Mặc định chỉ
    /// trả buổi sắp diễn — đặt `includeEnded=true` để xem cả buổi đã diễn.</summary>
    [HttpGet("by-performer/{performerId:guid}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PerformerDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPerformer(
        Guid performerId, [FromQuery] bool includeEnded = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetLoungeShowsByPerformerQuery(performerId, includeEnded, page, pageSize), ct);
        return Ok(ApiResponse<PerformerDetailDto>.Ok(result));
    }

    /// <summary>Mọi lựa chọn cho bộ lọc khám phá trong một lần gọi: thể loại nhạc, dòng nhạc, không
    /// gian, và danh sách thành phố. Thành phố lấy từ chính các buổi diễn đang có, nên bộ lọc không
    /// bày ra những nơi không có buổi nào.</summary>
    [HttpGet("filter-options")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<FilterOptionsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetFilterOptionsQuery(), ct);
        return Ok(ApiResponse<FilterOptionsDto>.Ok(result));
    }

    /// <summary>Buổi diễn của phòng trà người gọi VẬN HÀNH (mọi trạng thái, kể cả Draft): chủ thấy buổi của mình,
    /// nhân viên thấy buổi của phòng trà được phân công (OperatedShows, MLACP-466) — lọc theo `status` nếu có.
    /// RequireVenueOperator chứ không phải RequireOwner: MLACP-466 đã cho handler hiểu vai nhân viên nhưng route
    /// vẫn chặn nhân viên ở cửa (403), nên nửa sửa đó không bao giờ tới được họ.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
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
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<LoungeShowDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeShowDetailQuery(id), ct);
        return Ok(ApiResponse<LoungeShowDetailDto>.Ok(result));
    }

    /// <summary>Bản đồ khu vực chỗ ngồi cho 1 show — chỉ gồm zone có ít nhất 1 hạng vé trong show
    /// này (không phải toàn bộ zone của venue), kèm số chỗ còn trống/khoảng giá live-computed.
    /// Draft ẩn giống GetDetail (404 với người ngoài, kể cả Owner venue khác).</summary>
    [HttpGet("{id:guid}/seating-map")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<SeatingMapDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatingMap(Guid id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowSeatingMapQuery(id), ct);
        return Ok(ApiResponse<SeatingMapDto>.Ok(result));
    }

    /// <summary>Tối đa 6 buổi diễn "tương tự" cho trang chi tiết — cùng phòng trà HOẶC chung ít nhất 1
    /// thể loại nhạc với buổi diễn đang xem, luôn loại trừ chính buổi diễn đó, chỉ show Published/
    /// Ongoing. Ưu tiên show khớp cả 2 tiêu chí trước, còn lại theo ngày diễn gần nhất.</summary>
    [HttpGet("{id:guid}/similar")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilar(Guid id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetSimilarLoungeShowsQuery(id), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeShowListItemDto>>.Ok(result));
    }

    /// <summary>Thống kê vé đã bán của buổi diễn cho Owner: tổng vé, doanh thu, số vé đã check-in,
    /// và breakdown theo từng mức giá — đếm trực tiếp trên bảng Ticket tại thời điểm gọi (không
    /// dùng field đếm sẵn nào) nên luôn phản ánh đúng thời điểm hiện tại. Chỉ Owner của venue (hoặc
    /// Admin) xem được (403 nếu khác).</summary>
    [HttpGet("{id:guid}/ticket-stats")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<ShowTicketStatsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketStats(Guid id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowTicketStatsQuery(id), ct);
        return Ok(ApiResponse<ShowTicketStatsDto>.Ok(result));
    }

    /// <summary>Chỉ sửa được khi buổi diễn còn ở trạng thái Draft (422 nếu đã gửi duyệt/đã đăng);
    /// chỉ đúng Owner sở hữu venue mới sửa được (403 nếu khác). Ba trường chính sách hoàn vé
    /// (CancellationAllowed/RefundPercentage/CancellationDeadlineHours) theo ngữ nghĩa thay-thế như
    /// mọi trường khác của PUT này: bỏ trống là trả về mặc định (cho hủy, hoàn 100%, không hạn chót),
    /// không phải giữ nguyên giá trị cũ. Vì chỉ sửa được khi còn Draft nên chưa có vé nào bán theo
    /// chính sách bị ghi đè.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateLoungeShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateLoungeShowCommand(
            id, body.Name, body.Description, body.ScheduledStart, body.ScheduledEnd,
            body.TicketSaleClosesAt, body.CategoryId, body.OfflineQuota, body.OnlineQuota,
            body.CancellationAllowed, body.RefundPercentage, body.CancellationDeadlineHours), ct);
        return NoContent();
    }

    /// <summary>Hình thức phát của buổi hòa nhạc: "TwoD" (trình phát thường) hay "ThreeD" (phát
    /// trong không gian 3D). Giá trị này đã được trả ra trong chi tiết buổi diễn để client biết dựng
    /// trình phát nào, nhưng trước đây không có đường nào đặt nên mọi buổi diễn nằm im ở mặc định.
    /// Chỉ buổi diễn Online hoặc Hybrid mới đặt được ThreeD.</summary>
    [HttpPut("{id:guid}/playback-mode")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPlaybackMode(
        Guid id, [FromBody] SetPlaybackModeRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetPlaybackModeCommand(id, body.PlaybackMode), ct);
        return NoContent();
    }

    /// <summary>Danh sách người đã mua vé buổi hòa nhạc này, để chủ phòng trà đối soát và đón
    /// khách — chủ venue đó, nhân viên của venue đó (bản không có email, MLACP-592) hoặc Admin; 403 nếu khác, vì danh sách có tên và email người mua.
    /// Khác `GET {id}/ticket-stats` vốn chỉ trả con số tổng.</summary>
    [HttpGet("{id:guid}/orders")]
    [Authorize]
    [ProducesResponseType<ApiResponse<PaginatedResult<ShowOrderDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrders(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowOrdersQuery(id, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ShowOrderDto>>.Ok(result));
    }

    /// <summary>D18 (NĐ 144/2020 Điều 10): Owner khai báo số văn bản/liên kết "văn bản chấp thuận
    /// tổ chức biểu diễn" trước khi nộp duyệt — chỉ sửa được khi event còn Draft.</summary>
    [HttpPut("{id:guid}/legal-approval")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetLegalApprovalReference(
        Guid id, [FromBody] SetLegalApprovalReferenceRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLegalApprovalReferenceCommand(id, body.LegalApprovalReference), ct);
        return NoContent();
    }

    /// <summary>D19: Owner khai báo đã thanh toán tác quyền âm nhạc VCPMC — khác D18 (văn bản Nhà
    /// nước, phải nộp trước 7 ngày làm việc lúc nộp duyệt), hợp đồng li-xăng VCPMC là thỏa thuận
    /// dân sự nên có thể khai báo bất kỳ lúc nào trước khi show Ongoing/Ended/Cancelled — bắt buộc
    /// phải có trước khi bắt đầu show (xem StartLoungeShow).</summary>
    [HttpPut("{id:guid}/vcpmc-royalty")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetVcpmcRoyaltyReference(
        Guid id, [FromBody] SetVcpmcRoyaltyReferenceRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetVcpmcRoyaltyReferenceCommand(id, body.VcpmcRoyaltyReference), ct);
        return NoContent();
    }

    /// <summary>W02: tạo poster bằng AI (Gemini) — chỉ Owner có gói subscription bao gồm
    /// HasAiPoster. 2 giới hạn độc lập: MaxAiPostersPerMonth (hạn mức tính phí, chỉ tính lần thành
    /// công) và ai_poster_max_attempts_per_show (chống lạm dụng, tính mọi lần thử kể cả thất bại).
    /// 503 nếu vendor AI lỗi — lần thử thất bại KHÔNG bị trừ vào hạn mức.
    /// <para>MLACP-458: khi nền tảng đang chạy chế độ hàng đợi (ảnh do máy trạm sinh qua Google Flow, mất 50–90 giây),
    /// endpoint này trả <b>202</b> với <c>status = "Queued"</c> và <c>attemptId</c> thay vì ảnh; giao diện hỏi lại trạng
    /// thái qua lịch sử tạo poster, và chủ phòng trà nhận thông báo khi xong. Chế độ gọi thẳng vẫn trả 200 kèm ảnh như
    /// trước.</para></summary>
    [HttpPost("{id:guid}/ai-poster")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PosterGenerationResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<PosterGenerationResultDto>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GeneratePoster(
        Guid id, [FromBody] GeneratePosterRequest? body, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GeneratePosterCommand(id, body?.StyleHint), ct);
        var envelope = ApiResponse<PosterGenerationResultDto>.Ok(result);

        return result.Status == nameof(AiPosterGenerationStatus.Queued)
            ? Accepted(envelope)
            : Ok(envelope);
    }

    /// <summary>Đối chứng thủ công của tạo poster AI — Owner tự tải poster riêng thay vì dùng AI
    /// (hoặc không có gói subscription hỗ trợ tính năng này). Ghi đè và bỏ cờ PosterByAi nếu trước
    /// đó đã có poster do AI tạo.</summary>
    [HttpPut("{id:guid}/poster")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPoster(
        Guid id, [FromBody] SetShowPosterRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetShowPosterCommand(id, body.ImageUrl), ct);
        return NoContent();
    }

    /// <summary>Lịch sử các lần tạo poster AI của show — Owner tự tra được lần nào thành công, lần
    /// nào thất bại và vì sao, xác nhận lần thất bại không bị trừ hạn mức.</summary>
    [HttpGet("{id:guid}/ai-poster/history")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PosterGenerationAttemptDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosterGenerationHistory(Guid id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPosterGenerationHistoryQuery(id), ct);
        return Ok(ApiResponse<IReadOnlyList<PosterGenerationAttemptDto>>.Ok(result));
    }

    /// <summary>Xóa thật (hard delete) — chỉ áp dụng cho buổi diễn còn ở trạng thái Draft (422 nếu
    /// khác); buổi diễn đã publish/đang diễn ra phải dùng huỷ (Cancel), không xóa được nữa.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteLoungeShowCommand(id), ct);
        return NoContent();
    }

    /// <summary>Gửi duyệt: chuyển Draft → Pending, tạo bản ghi kiểm duyệt cho Admin. Bắt buộc đã có
    /// ≥1 hạng vé, ≥1 nghệ sĩ trong line-up, văn bản chấp thuận biểu diễn (NĐ 144/2020 Điều 10), và
    /// nộp trước tối thiểu N ngày làm việc so với ngày diễn — thiếu bất kỳ điều kiện nào trả về lỗi
    /// nêu rõ (422).</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new PublishLoungeShowCommand(id), ct);
        return NoContent();
    }

    /// <summary>Hủy buổi diễn đã đăng — vé đã Confirmed được hủy kèm tạo yêu cầu hoàn 100% tiền
    /// (RefundRequest) và thông báo tới từng người mua.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new CancelLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reschedule(
        Guid id, [FromBody] RescheduleLoungeShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RescheduleLoungeShowCommand(id, body.NewScheduledStart), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new StartLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/end")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> End(Guid id, CancellationToken ct = default)
    {
        await _sender.Send(new EndLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/format")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeFormat(
        Guid id, [FromBody] ChangeLoungeShowFormatRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ChangeLoungeShowFormatCommand(id, body.NewFormat), ct);
        return NoContent();
    }

    /// <summary>Thêm nghệ sĩ vào danh sách biểu diễn — khi buổi diễn chưa bắt đầu (nháp, chờ duyệt, đang mở bán; MLACP-622).
    /// Thêm nghệ sĩ không mở hoàn tiền cho người đã mua.
    /// Trả 409 nếu nghệ sĩ này đã có trong line-up của đúng buổi diễn này.</summary>
    [HttpPost("{id:guid}/performances")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddPerformance(
        Guid id, [FromBody] AddPerformanceRequest body, CancellationToken ct = default)
    {
        var performanceId = await _sender.Send(new AddPerformanceCommand(
            id, body.PerformerId, body.PerformerName, body.Role,
            body.OrderIndex, body.SetTime, body.AcceptsDonation), ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1.0" }, ApiResponse<Guid>.Ok(performanceId));
    }

    /// <summary>Sửa vai trò/thứ tự/giờ diễn/bật-tắt nhận donate của 1 nghệ sĩ trong line-up — khi buổi diễn chưa bắt đầu
    /// (MLACP-622). Hạ nghệ sĩ chính xuống vai khác trên buổi đang mở bán: bắt buộc <c>changeReason</c>, người đã mua được
    /// báo và được hoàn 100%. Đổi sang nghệ sĩ khác: xóa rồi thêm lại.</summary>
    [HttpPut("{id:guid}/performances/{performanceId:guid}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePerformance(
        Guid id, Guid performanceId, [FromBody] UpdatePerformanceRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdatePerformanceCommand(
            performanceId, body.Role, body.OrderIndex, body.SetTime, body.AcceptsDonation, body.ChangeReason), ct);
        return NoContent();
    }

    /// <summary>Xóa 1 nghệ sĩ khỏi danh sách biểu diễn — khi buổi diễn chưa bắt đầu (MLACP-622). Buổi đang mở bán: bắt
    /// buộc <c>changeReason</c> (query), người đã mua được báo và được hoàn 100% (NĐ 144/2020 Điều 10 khoản 4 điểm d;
    /// Luật BVQLNTD 2023). 409 nếu tiết mục đã nhận tiền ủng hộ.</summary>
    [HttpDelete("{id:guid}/performances/{performanceId:guid}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeletePerformance(
        Guid id, Guid performanceId, [FromQuery] string? changeReason = null, CancellationToken ct = default)
    {
        await _sender.Send(new DeletePerformanceCommand(performanceId, changeReason), ct);
        return NoContent();
    }

    /// <summary>Chấm sao 1-5 + nhận xét sau khi show kết thúc — chỉ mở trong 7 ngày kể từ lúc kết
    /// thúc (§6.13). Bắt buộc đã "check-in" thật (vé vào cửa đã quét QR, hoặc vé xem livestream đã
    /// từng thực sự nhận được URL phát — xem RateShowCommandHandler), không chỉ cần có vé Confirmed.
    /// Mỗi người chỉ đánh giá được 1 lần cho 1 show (409 nếu đã đánh giá).</summary>
    [HttpPost("{id:guid}/rate")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rate(
        Guid id, [FromBody] RateShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RateShowCommand(id, body.Score, body.Comment), ct);
        return NoContent();
    }

    /// <summary>Danh sách đánh giá công khai của 1 buổi diễn — điểm trung bình + phân bố sao (1-5)
    /// tính trên toàn bộ đánh giá còn hiệu lực, danh sách nhận xét phân trang sắp mới nhất lên
    /// trước. Đánh giá đã bị Admin gỡ (IsRemoved) không tính vào điểm trung bình/phân bố và không
    /// xuất hiện trong danh sách. <c>score</c> (1–5, tuỳ chọn) chỉ lọc danh sách nhận xét; tổng quan không đổi
    /// (MLACP-573).</summary>
    [HttpGet("{id:guid}/ratings")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<ShowRatingsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRatings(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? score = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowRatingsQuery(id, page, pageSize, score), ct);
        return Ok(ApiResponse<ShowRatingsDto>.Ok(result));
    }
}

public sealed record SetPlaybackModeRequest(string PlaybackMode);
public sealed record RescheduleLoungeShowRequest(DateTimeOffset NewScheduledStart);
public sealed record ChangeLoungeShowFormatRequest(LoungeShowFormat NewFormat);

public sealed record UpdateLoungeShowRequest(
    string Name,
    string Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    DateTimeOffset? TicketSaleClosesAt,
    Guid? CategoryId,
    int? OfflineQuota,
    int? OnlineQuota,
    bool? CancellationAllowed = null,
    decimal? RefundPercentage = null,
    int? CancellationDeadlineHours = null);

public sealed record AddPerformanceRequest(
    Guid? PerformerId,
    string? PerformerName,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation);

public sealed record UpdatePerformanceRequest(
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation,
    string? ChangeReason = null);

public sealed record RateShowRequest(int Score, string? Comment);

public sealed record SetLegalApprovalReferenceRequest(string LegalApprovalReference);

public sealed record SetVcpmcRoyaltyReferenceRequest(string VcpmcRoyaltyReference);

public sealed record GeneratePosterRequest(string? StyleHint);

public sealed record SetShowPosterRequest(string ImageUrl);
