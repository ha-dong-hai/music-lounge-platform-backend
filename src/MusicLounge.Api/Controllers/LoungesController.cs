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
using MusicLounge.Application.Lounges.Commands.RemoveLoungeGalleryImage;
using MusicLounge.Application.Lounges.Commands.RemoveVenueTourHotspot;
using MusicLounge.Application.Lounges.Commands.RemoveVenueTourScene;
using MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;
using MusicLounge.Application.Lounges.Commands.SetLoungeBusinessLicense;
using MusicLounge.Application.Lounges.Commands.SetLoungeImage;
using MusicLounge.Application.Lounges.Commands.SetLoungeModel3D;
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

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lounges")]
public sealed class LoungesController : ControllerBase
{
    private readonly ISender _sender;

    public LoungesController(ISender sender) => _sender = sender;

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

    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLoungeCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1.0" }, ApiResponse<int>.Ok(id));
    }

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

    [HttpPut("{id:int}/business-license")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBusinessLicense(
        int id, [FromBody] SetBusinessLicenseRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLoungeBusinessLicenseCommand(id, body.DocumentUrl), ct);
        return NoContent();
    }

    /// <summary>Tour ảo 3D: Owner gắn/gỡ file .glb thật cho phòng trà. modelUrl = null → dùng scene mẫu mặc định.</summary>
    [HttpPut("{id:int}/model-3d")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetModel3D(
        int id, [FromBody] SetModel3DRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLoungeModel3DCommand(id, body.ModelUrl), ct);
        return NoContent();
    }

    [HttpGet("{id:int}/zones")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SeatingZoneDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones(
        int id, [FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeZonesQuery(id, activeOnly), ct);
        return Ok(ApiResponse<IReadOnlyList<SeatingZoneDto>>.Ok(result));
    }

    [HttpPost("{id:int}/zones")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateZone(
        int id, [FromBody] CreateZoneRequest body, CancellationToken ct = default)
    {
        var zoneId = await _sender.Send(new CreateSeatingZoneCommand(id, body.Name, body.Description, body.Capacity), ct);
        return CreatedAtAction(nameof(GetZones), new { id, version = "1.0" }, ApiResponse<int>.Ok(zoneId));
    }

    [HttpPut("{id:int}/zones/{zoneId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateZone(
        int id, int zoneId, [FromBody] UpdateZoneRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateSeatingZoneCommand(zoneId, body.Name, body.Description, body.Capacity), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/zones/{zoneId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateZone(
        int id, int zoneId, CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateSeatingZoneCommand(zoneId), ct);
        return NoContent();
    }

    /// <summary>Bản đồ chọn khu vực 2D: Owner vẽ vị trí/kích thước/màu 1 khu trên sơ đồ (% tọa độ).</summary>
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

    /// <summary>Bản đồ chọn khu vực 3D: Owner gắn/gỡ marker vị trí 1 khu trong không gian 3D. Null cả 3 = gỡ.</summary>
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

    /// <summary>Ảnh sơ đồ mặt bằng thật (tham chiếu để Owner vẽ khu vực lên trên). Null = xóa.</summary>
    [HttpPut("{id:int}/area-layout-image")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAreaLayoutImage(
        int id, [FromBody] SetAreaLayoutImageRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLoungeAreaLayoutImageCommand(id, body.ImageUrl), ct);
        return NoContent();
    }

    // Tour ảo 360° kiểu Louvre/Bảo tàng TPHCM: nhiều ảnh panorama (scene) do Owner tự chụp/upload,
    // nối với nhau qua hotspot. Khác hoàn toàn với model-3d ở trên (1 file .glb dựng tay cho scene
    // mẫu dựng code) — đây là ảnh chụp thật của không gian quán, không thay thế cho nhau.
    [HttpGet("{id:int}/tour")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<VenueTourDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTour(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetVenueTourQuery(id), ct);
        return Ok(ApiResponse<VenueTourDto>.Ok(result));
    }

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
        return CreatedAtAction(nameof(GetTour), new { id, version = "1.0" }, ApiResponse<int>.Ok(sceneId));
    }

    // Alternative to AddTourScene for Owners without a native 360° capture app: submit several
    // overlapping photos taken standing in one spot while rotating, the standalone panorama-
    // stitcher service merges them into one panorama, and that becomes a scene exactly like
    // AddTourScene's — counts against the same MaxTourScenes quota, plus its own anti-abuse cap
    // since stitching runs on our own server's CPU.
    //
    // Runs in the background (StitchVenueTourSceneJob) — a stitch can take 15-30+ seconds and
    // occasionally brushes the panorama-stitcher HttpClient's 120s timeout on harder photo sets.
    // This returns 202 with an attempt id immediately; poll GetStitchAttempt for the outcome.
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
        return AcceptedAtAction(
            nameof(GetStitchAttempt), new { id, attemptId, version = "1.0" }, ApiResponse<int>.Ok(attemptId));
    }

    // Owner polls this after StitchTourScene returns an attempt id. Status is "Pending" while the
    // background job hasn't finished yet, "Succeeded" (ResultSceneId set) or "Failed"
    // (ErrorMessage set) once it has.
    [HttpGet("{id:int}/tour/scenes/stitch/{attemptId:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<VenueTourStitchAttemptDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStitchAttempt(int id, int attemptId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetVenueTourStitchAttemptQuery(id, attemptId), ct);
        return Ok(ApiResponse<VenueTourStitchAttemptDto>.Ok(result));
    }

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

    /// <summary>Vị trí marker của 1 scene trên bản đồ tổng quan (AreaLayoutImageUrl). Null cả X/Y = gỡ.</summary>
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
        return CreatedAtAction(nameof(GetTour), new { id, version = "1.0" }, ApiResponse<int>.Ok(hotspotId));
    }

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

    // Nhiều ảnh showcase cho trang chi tiết venue — khác PrimaryImageUrl (1 ảnh đại diện dùng ở
    // danh sách) và khác VenueTourScene (ảnh panorama 360° dùng để điều hướng không gian, có giới
    // hạn theo gói). Miễn phí cho mọi Owner, không giới hạn số lượng theo gói subscription.
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
}

public sealed record AssignStaffRequest(int UserId);

public sealed record SetLoungeImageRequest(string ImageUrl);

public sealed record SetBusinessLicenseRequest(string DocumentUrl);

public sealed record SetModel3DRequest(string? ModelUrl);

public sealed record CreateZoneRequest(string Name, string? Description, int Capacity);

public sealed record UpdateZoneRequest(string Name, string? Description, int Capacity);

public sealed record SetZoneLayout2DRequest(
    double X, double Y, double Width, double Height, double RotationDeg, string? Color);

public sealed record SetZoneLayout3DRequest(double? X, double? Y, double? Z);

public sealed record SetAreaLayoutImageRequest(string? ImageUrl);

public sealed record AddVenueTourSceneRequest(string ImageUrl, string? Name);

public sealed record AddVenueTourHotspotRequest(
    string Type, double Yaw, double Pitch, string? Label, int? TargetSceneId, string? InfoText);

public sealed record StitchVenueTourSceneRequest(IReadOnlyList<string> SourceImageUrls, string? Name);

public sealed record AddLoungeGalleryImageRequest(string ImageUrl, string? Caption);

public sealed record SetVenueTourScenePositionRequest(double? X, double? Y);

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
