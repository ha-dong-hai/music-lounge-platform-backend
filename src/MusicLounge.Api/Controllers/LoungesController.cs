using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.Commands.AddLoungeGalleryImage;
using MusicLounge.Application.Lounges.Commands.AddVenueTourHotspot;
using MusicLounge.Application.Lounges.Commands.AddVenueTourScene;
using MusicLounge.Application.Lounges.Commands.CreateLounge;
using MusicLounge.Application.Lounges.Commands.CreateSeatingZone;
using MusicLounge.Application.Lounges.Commands.DeactivateSeatingZone;
using MusicLounge.Application.Lounges.Commands.DeleteLounge;
using MusicLounge.Application.Lounges.Commands.RemoveLoungeGalleryImage;
using MusicLounge.Application.Lounges.Commands.RemoveVenueTourHotspot;
using MusicLounge.Application.Lounges.Commands.RemoveVenueTourScene;
using MusicLounge.Application.Lounges.Commands.ReorderLoungeGalleryImages;
using MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;
using MusicLounge.Application.Lounges.Commands.SetLoungeImage;
using MusicLounge.Application.Lounges.Commands.SetVenueTourScenePosition;
using MusicLounge.Application.Lounges.Commands.SetZoneLayout2D;
using MusicLounge.Application.Lounges.Commands.SetZoneLayout3D;
using MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;
using MusicLounge.Application.Lounges.Commands.UpdateLounge;
using MusicLounge.Application.Lounges.Commands.UpdateSeatingZone;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Application.Lounges.Queries.GetLoungeDetail;
using MusicLounge.Application.Lounges.Queries.GetLounges;
using MusicLounge.Application.Lounges.Queries.GetLoungeZones;
using MusicLounge.Application.Lounges.Queries.GetVenueTour;
using MusicLounge.Application.Lounges.Queries.GetVenueTourStitchAttempt;
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

    /// <summary>Thêm khu vực chỗ ngồi mới cho venue (tên/sức chứa/mô tả).</summary>
    [HttpPost("{id:int}/zones")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateZone(
        int id, [FromBody] CreateSeatingZoneRequest body, CancellationToken ct = default)
    {
        var zoneId = await _sender.Send(
            new CreateSeatingZoneCommand(id, body.Name, body.Description, body.Capacity), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(zoneId));
    }

    /// <summary>Cập nhật tên/sức chứa/mô tả của 1 khu vực chỗ ngồi.</summary>
    [HttpPut("zones/{zoneId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateZone(
        int zoneId, [FromBody] UpdateSeatingZoneRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateSeatingZoneCommand(zoneId, body.Name, body.Description, body.Capacity), ct);
        return NoContent();
    }

    /// <summary>Ngưng dùng 1 khu vực chỗ ngồi — không xóa cứng: một zone từng được dùng để bán vé
    /// vẫn phải giữ nguyên tham chiếu lịch sử (mức giá vé cũ, đơn hàng cũ) nên chỉ đánh dấu ngưng
    /// hoạt động (IsActive=false, ẩn khỏi GetZones khi activeOnly=true), khớp đúng thiết kế IsActive
    /// đã có sẵn trên entity từ trước.</summary>
    [HttpDelete("zones/{zoneId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateZone(int zoneId, CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateSeatingZoneCommand(zoneId), ct);
        return NoContent();
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

    [HttpPut("{id:int}/zones/{zoneId:int}/layout-2d")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetZoneLayout2D(
        int id, int zoneId, [FromBody] SetZoneLayout2DRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetZoneLayout2DCommand(
            zoneId, body.X, body.Y, body.Width, body.Height, body.RotationDeg, body.Color), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/zones/{zoneId:int}/layout-3d")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetZoneLayout3D(
        int id, int zoneId, [FromBody] SetZoneLayout3DRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetZoneLayout3DCommand(zoneId, body.X, body.Y, body.Z), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/area-layout-image")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAreaLayoutImage(
        int id, [FromBody] SetAreaLayoutImageRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLoungeAreaLayoutImageCommand(id, body.ImageUrl), ct);
        return NoContent();
    }

    // ---- Tour ảo 360° ----

    /// <summary>Tour ảo 360° kiểu Louvre/bảo tàng — công khai, không cần đăng nhập (khán giả xem
    /// trước khi mua vé), khác Model3DUrl (1 file .glb duy nhất).</summary>
    [HttpGet("{id:int}/tour")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<VenueTourDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTour(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetVenueTourQuery(id), ct);
        return Ok(ApiResponse<VenueTourDto>.Ok(result));
    }

    /// <summary>Thêm 1 scene panorama đã chụp sẵn (upload qua POST /uploads/images trước) — giới
    /// hạn theo MaxTourScenes của gói subscription đang hoạt động (chụp tại thời điểm subscribe,
    /// D12).</summary>
    [HttpPost("{id:int}/tour/scenes")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddTourScene(
        int id, [FromBody] AddVenueTourSceneRequest body, CancellationToken ct = default)
    {
        var sceneId = await _sender.Send(new AddVenueTourSceneCommand(id, body.ImageUrl, body.Name), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(sceneId));
    }

    /// <summary>Ghép nhiều ảnh chụp xoay vòng thành 1 panorama qua microservice panorama-stitcher
    /// riêng — dành cho Owner không có app chụp 360° gốc. Chạy nền (StitchVenueTourSceneJob), trả
    /// 202 kèm id lần thử để tự tra kết quả qua GET .../stitch/{attemptId}. Cùng giới hạn
    /// MaxTourScenes như thêm ảnh trực tiếp, cộng thêm giới hạn chống lạm dụng riêng (ghép ảnh tốn
    /// CPU server, không như gọi vendor AI trả phí).</summary>
    [HttpPost("{id:int}/tour/scenes/stitch")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StitchTourScene(
        int id, [FromBody] StitchVenueTourSceneRequest body, CancellationToken ct = default)
    {
        var attemptId = await _sender.Send(
            new StitchVenueTourSceneCommand(id, body.SourceImageUrls, body.Name), ct);
        return StatusCode(StatusCodes.Status202Accepted, ApiResponse<int>.Ok(attemptId));
    }

    /// <summary>Owner tự tra kết quả 1 lần ghép ảnh đã gửi (Pending/Succeeded/Failed).</summary>
    [HttpGet("{id:int}/tour/scenes/stitch/{attemptId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<VenueTourStitchAttemptDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTourStitchAttempt(
        int id, int attemptId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetVenueTourStitchAttemptQuery(id, attemptId), ct);
        return Ok(ApiResponse<VenueTourStitchAttemptDto>.Ok(result));
    }

    /// <summary>Xóa 1 scene — dọn luôn mọi hotspot ở scene khác đang trỏ (Navigate) tới scene này,
    /// tránh vi phạm FK.</summary>
    [HttpDelete("{id:int}/tour/scenes/{sceneId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTourScene(int id, int sceneId, CancellationToken ct = default)
    {
        await _sender.Send(new RemoveVenueTourSceneCommand(id, sceneId), ct);
        return NoContent();
    }

    /// <summary>Đặt vị trí đánh dấu của 1 scene trên ảnh mặt bằng (area-layout-image) — X/Y theo %
    /// (0-100). Truyền cả 2 null để xóa vị trí đã đặt.</summary>
    [HttpPut("{id:int}/tour/scenes/{sceneId:int}/position")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTourScenePosition(
        int id, int sceneId, [FromBody] SetVenueTourScenePositionRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetVenueTourScenePositionCommand(id, sceneId, body.X, body.Y), ct);
        return NoContent();
    }

    /// <summary>Thêm hotspot vào 1 scene — Navigate (dẫn sang scene khác, bắt buộc TargetSceneId)
    /// hoặc Info (hiện chú thích tĩnh).</summary>
    [HttpPost("{id:int}/tour/scenes/{sceneId:int}/hotspots")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTourHotspot(
        int id, int sceneId, [FromBody] AddVenueTourHotspotRequest body, CancellationToken ct = default)
    {
        var hotspotId = await _sender.Send(new AddVenueTourHotspotCommand(
            id, sceneId, body.Type, body.Yaw, body.Pitch, body.Label, body.TargetSceneId, body.InfoText), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(hotspotId));
    }

    /// <summary>Xóa 1 hotspot.</summary>
    [HttpDelete("{id:int}/tour/hotspots/{hotspotId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTourHotspot(int id, int hotspotId, CancellationToken ct = default)
    {
        await _sender.Send(new RemoveVenueTourHotspotCommand(id, hotspotId), ct);
        return NoContent();
    }
}

public sealed record ReorderGalleryImagesRequest(List<int> OrderedImageIds);

public sealed record AddLoungeGalleryImageRequest(string ImageUrl, string? Caption);

public sealed record AddVenueTourSceneRequest(string ImageUrl, string? Name);

public sealed record StitchVenueTourSceneRequest(IReadOnlyList<string> SourceImageUrls, string? Name);

public sealed record SetVenueTourScenePositionRequest(double? X, double? Y);

public sealed record AddVenueTourHotspotRequest(
    string Type, double Yaw, double Pitch, string? Label, int? TargetSceneId, string? InfoText);

public sealed record CreateSeatingZoneRequest(string Name, string? Description, int Capacity);

public sealed record UpdateSeatingZoneRequest(string Name, string? Description, int Capacity);

public sealed record SetZoneLayout2DRequest(
    double X, double Y, double Width, double Height, double RotationDeg, string? Color);
public sealed record SetZoneLayout3DRequest(double? X, double? Y, double? Z);
public sealed record SetAreaLayoutImageRequest(string? ImageUrl);

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
