using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.Commands.CreateLounge;
using MusicLounge.Application.Lounges.Commands.DeleteLounge;
using MusicLounge.Application.Lounges.Commands.UpdateLounge;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Application.Lounges.Queries.GetLoungeDetail;
using MusicLounge.Application.Lounges.Queries.GetLounges;
using MusicLounge.Application.Lounges.Queries.GetLoungeZones;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau se chi them method vao file nay, khong tao lai.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lounges")]
public sealed class LoungesController : ControllerBase
{
    private readonly ISender _sender;

    public LoungesController(ISender sender) => _sender = sender;

    /// <summary>Public khi mine=false (lọc theo city); can dang nhap khi mine=true (chi tra phong
    /// tra cua chinh Owner dang goi).</summary>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? city = null,
        [FromQuery] bool mine = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungesQuery(city, mine, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeListItemDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<LoungeDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeDetailQuery(id), ct);
        return Ok(ApiResponse<LoungeDetailDto>.Ok(result));
    }

    /// <summary>Danh sách khu vực chỗ ngồi — tách riêng khỏi GetDetail vì LoungeDetailDto không
    /// mang theo zones, khớp đúng cách local master đã thiết kế.</summary>
    [HttpGet("{id:int}/zones")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SeatingZoneDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones(
        int id, [FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeZonesQuery(id, activeOnly), ct);
        return Ok(ApiResponse<IReadOnlyList<SeatingZoneDto>>.Ok(result));
    }

    /// <summary>Chỉ Chủ phòng trà (Owner) tạo được — phòng trà mới luôn ở trạng thái chờ Admin duyệt
    /// (LoungeStatus.Pending mặc định).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLoungeCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }

    /// <summary>Chỉ đúng Owner sở hữu (hoặc Admin) mới sửa được — người khác nhận 403.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateLoungeRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateLoungeCommand(
            id, body.Name, body.Description, body.AtmosphereId,
            body.Street, body.Ward, body.District, body.City, body.Latitude, body.Longitude), ct);
        return NoContent();
    }

    /// <summary>Chỉ đúng Owner sở hữu (hoặc Admin) mới xóa được; bị chặn (409) nếu phòng trà còn
    /// bất kỳ sự kiện nào (mọi trạng thái, tránh mất lịch sử show đã kết thúc/hủy).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeleteLoungeCommand(id), ct);
        return NoContent();
    }
}

public sealed record UpdateLoungeRequest(
    string Name,
    string? Description,
    int? AtmosphereId,
    string Street,
    string Ward,
    string District,
    string City,
    double? Latitude,
    double? Longitude);
