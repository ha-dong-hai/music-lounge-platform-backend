using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.Commands.AddLoungeGalleryImage;
using MusicLounge.Application.Lounges.Commands.CreateLounge;
using MusicLounge.Application.Lounges.Commands.DeleteLounge;
using MusicLounge.Application.Lounges.Commands.RemoveLoungeGalleryImage;
using MusicLounge.Application.Lounges.Commands.ReorderLoungeGalleryImages;
using MusicLounge.Application.Lounges.Commands.SetLoungeImage;
using MusicLounge.Application.Lounges.Commands.UpdateLounge;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Application.Lounges.Queries.GetLoungeDetail;
using MusicLounge.Application.Lounges.Queries.GetLounges;
using MusicLounge.Application.Lounges.Queries.GetLoungeZones;
using MusicLounge.Application.Staffing.Commands.AssignStaff;
using MusicLounge.Application.Staffing.Commands.DeactivateStaff;
using MusicLounge.Application.Staffing.DTOs;
using MusicLounge.Application.Staffing.Queries.FindUserByEmail;
using MusicLounge.Application.Staffing.Queries.GetLoungeStaff;

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
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLoungeCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1.0" }, ApiResponse<int>.Ok(id));
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

    [HttpGet("{id:int}/staff")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeStaffDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaff(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeStaffQuery(id), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeStaffDto>>.Ok(result));
    }

    [HttpGet("staff/lookup")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<UserLookupDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupUserByEmail([FromQuery] string email, CancellationToken ct = default)
    {
        var result = await _sender.Send(new FindUserByEmailQuery(email), ct);
        return Ok(ApiResponse<UserLookupDto>.Ok(result));
    }

    [HttpPost("{id:int}/staff")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignStaff(
        int id, [FromBody] AssignStaffRequest body, CancellationToken ct = default)
    {
        var staffId = await _sender.Send(new AssignStaffCommand(id, body.UserId), ct);
        return CreatedAtAction(nameof(GetStaff), new { id, version = "1.0" }, ApiResponse<int>.Ok(staffId));
    }

    [HttpDelete("{id:int}/staff/{staffId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateStaff(
        int id, int staffId, CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateStaffCommand(staffId), ct);
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

    /// <summary>Ảnh đầu tiên tự động là đại diện (PrimaryImageUrl) — xem
    /// AddLoungeGalleryImageCommandHandler. Upload file thật qua POST /uploads/images trước, lấy
    /// URL trả về rồi mới gọi endpoint này.</summary>
    [HttpPost("{id:int}/gallery")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGalleryImage(
        int id, [FromBody] AddLoungeGalleryImageRequest body, CancellationToken ct = default)
    {
        var imageId = await _sender.Send(new AddLoungeGalleryImageCommand(id, body.ImageUrl, body.Caption), ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1.0" }, ApiResponse<int>.Ok(imageId));
    }

    /// <summary>Owner tự chọn đổi ảnh đại diện sang 1 ảnh khác (thường lấy từ gallery đã upload).</summary>
    [HttpPut("{id:int}/image")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetImage(
        int id, [FromBody] SetLoungeImageRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLoungeImageCommand(id, body.ImageUrl), ct);
        return NoContent();
    }

    /// <summary>Chỉ đúng Owner sở hữu (hoặc Admin) mới xóa được ảnh.</summary>
    [HttpDelete("{id:int}/gallery/{imageId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGalleryImage(int id, int imageId, CancellationToken ct = default)
    {
        await _sender.Send(new RemoveLoungeGalleryImageCommand(id, imageId), ct);
        return NoContent();
    }

    /// <summary>Body phải là hoán vị đầy đủ Id các ảnh hiện có của phòng trà (thiếu/dư/lạc Id đều
    /// bị từ chối) — vị trí trong mảng chính là thứ tự hiển thị mới.</summary>
    [HttpPut("{id:int}/gallery/order")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReorderGalleryImages(
        int id, [FromBody] ReorderGalleryImagesRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ReorderLoungeGalleryImagesCommand(id, body.OrderedImageIds), ct);
        return NoContent();
    }
}

public sealed record ReorderGalleryImagesRequest(List<int> OrderedImageIds);

public sealed record AddLoungeGalleryImageRequest(string ImageUrl, string? Caption);

public sealed record SetLoungeImageRequest(string ImageUrl);

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

public sealed record AssignStaffRequest(int UserId);
