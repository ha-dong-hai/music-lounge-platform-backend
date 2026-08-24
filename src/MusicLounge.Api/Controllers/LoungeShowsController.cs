using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;
using MusicLounge.Application.LoungeShows.Queries.GetMyLoungeShows;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (sua, publish...) se chi them method vao file nay.
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
}
