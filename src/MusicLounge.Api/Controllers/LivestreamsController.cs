using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Livestreams.Commands.CreateLivestream;
using MusicLounge.Application.Livestreams.Commands.EndLivestream;
using MusicLounge.Application.Livestreams.Commands.ProcessMuxWebhook;
using MusicLounge.Application.Livestreams.Commands.SetChatEnabled;
using MusicLounge.Application.Livestreams.Commands.StartLivestream;
using MusicLounge.Application.Livestreams.Commands.TerminateLivestream;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Application.Livestreams.Queries.GetChatHistory;
using MusicLounge.Application.Livestreams.Queries.GetLivestreamCredentials;
using MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (PPV...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/livestreams")]
public sealed class LivestreamsController : ControllerBase
{
    private readonly ISender _sender;

    public LivestreamsController(ISender sender) => _sender = sender;

    /// <summary>Chi tiết livestream + kiểm soát quyền xem PPV: Admin và Owner/Staff của đúng venue
    /// luôn xem được (giám sát stream); stream miễn phí (IsFree) cho mọi khán giả đã đăng nhập xem
    /// mà không cần vé; stream PPV chỉ trả `HlsUrl` cho người có vé Livestream-tier Confirmed cho
    /// đúng show này (`UserHasAccess=false`, `HlsUrl=null` nếu không có vé — không lộ URL phát).
    /// Access token của vé PPV lấy qua GET /tickets/{id} (LivestreamDetail.AccessToken), không lặp
    /// lại ở đây.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<LivestreamDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLivestreamDetailQuery(id), ct);
        return Ok(ApiResponse<LivestreamDetailDto>.Ok(result));
    }

    /// <summary>Owner/Staff của venue tạo phiên livestream cho 1 show — mỗi show chỉ có đúng 1
    /// livestream (409 nếu đã tồn tại). Hệ thống tự sinh stream key bí mật qua provider đang active
    /// (Mux/Agora/Cloudflare); lấy lại stream key sau này qua GET /{id}/credentials.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateLivestreamCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Created($"api/v1/livestreams/{id}", ApiResponse<int>.Ok(id));
    }

    /// <summary>Owner/Staff bắt đầu phát sóng — yêu cầu Admin đã duyệt livestream (W08) và show đã
    /// khai báo tác quyền VCPMC (D19); ghi nhận thời điểm bắt đầu, đồng bộ show sang Ongoing, và
    /// thông báo tới người đã mua vé/theo dõi venue.</summary>
    [HttpPost("{id:int}/start")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Start(int id, CancellationToken ct = default)
    {
        await _sender.Send(new StartLivestreamCommand(id), ct);
        return NoContent();
    }

    /// <summary>Owner/Staff kết thúc phát sóng — ghi nhận thời điểm kết thúc, đồng bộ show sang
    /// Ended và mở cửa sổ đánh giá (§6.13).</summary>
    [HttpPost("{id:int}/end")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> End(int id, CancellationToken ct = default)
    {
        await _sender.Send(new EndLivestreamCommand(id), ct);
        return NoContent();
    }

    /// <summary>Staff/Admin của venue xem RTMP URL + Stream Key để cắm OBS. Không lộ ra viewer.</summary>
    [HttpGet("{id:int}/credentials")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ApiResponse<LivestreamCredentialsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentials(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLivestreamCredentialsQuery(id), ct);
        return Ok(ApiResponse<LivestreamCredentialsDto>.Ok(result));
    }

    /// <summary>Lịch sử chat của livestream (phân trang) — dùng để khán giả vào xem trễ bắt kịp
    /// nội dung đã trôi qua; tin nhắn mới trong lúc đang xem đến qua kênh realtime của
    /// LivestreamHub (SignalR), không qua endpoint này. Cùng quy tắc quyền xem như GetDetail: stream
    /// miễn phí cho mọi khán giả đã đăng nhập, stream PPV chỉ cho người có vé.</summary>
    [HttpGet("{id:int}/chat")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ChatMessageDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatHistory(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetChatHistoryQuery(id, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ChatMessageDto>>.Ok(result));
    }

    /// <summary>Owner/Staff bật/tắt chat cho livestream — có hiệu lực ngay: mọi tin nhắn gửi qua
    /// LivestreamHub sau lệnh này đều được kiểm tra lại giá trị mới nhất, không có độ trễ cache.</summary>
    [HttpPost("{id:int}/chat-enabled")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetChatEnabled(
        int id, [FromBody] SetChatEnabledRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetChatEnabledCommand(id, body.Enabled), ct);
        return NoContent();
    }

    /// <summary>Nhận webhook từ Mux (đăng ký URL này trong Mux Dashboard > Settings > Webhooks) khi
    /// stream chuyển trạng thái. Xác thực bằng header Mux-Signature (HMAC-SHA256), không dùng JWT —
    /// gọi trực tiếp từ hạ tầng Mux, không qua trình duyệt người dùng. Chỉ tự động đóng livestream
    /// khi Mux xác nhận encoder đã ngắt hẳn (video.live_stream.idle) và hệ thống đang ghi nhận Live —
    /// KHÔNG tự động mở (video.live_stream.active) để không bỏ qua cổng kiểm duyệt Admin (W08) và
    /// yêu cầu khai báo tác quyền VCPMC (D19) mà endpoint /start đang bắt buộc. Mux không trả về số
    /// người xem trong webhook — số liệu đó lấy từ LivestreamHub (SignalR), không phải từ đây.</summary>
    [HttpPost("webhooks/mux")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MuxWebhook(CancellationToken ct = default)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Mux-Signature"].ToString();

        var verified = await _sender.Send(new ProcessMuxWebhookCommand(rawBody, signature), ct);
        return verified ? Ok() : Unauthorized();
    }

    /// <summary>W22 — Admin dừng ngay 1 livestream đang phát vì vi phạm nội dung. Ghi lại
    /// người dừng (TerminatedById) + lý do (TerminatedReason), chuyển trạng thái sang Terminated —
    /// trạng thái cuối, Start/End không còn nhận vào từ đây nên stream không thể tiếp tục. Đồng bộ
    /// show sang Ended. Thông báo mọi viewer đang kết nối qua LivestreamHub để client ngừng gọi lại
    /// HLS endpoint.</summary>
    [HttpPost("{id:int}/terminate")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Terminate(
        int id, [FromBody] TerminateLivestreamRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new TerminateLivestreamCommand(id, body.Reason), ct);
        return NoContent();
    }
}

public sealed record TerminateLivestreamRequest(string Reason);
public sealed record SetChatEnabledRequest(bool Enabled);
