using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (xem danh sach/chi tiet, sua, publish...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lounge-shows")]
public sealed class LoungeShowsController : ControllerBase
{
    private readonly ISender _sender;

    public LoungeShowsController(ISender sender) => _sender = sender;

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
}
