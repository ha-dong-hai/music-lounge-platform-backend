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
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.Commands.ReviewShow;

namespace MusicLounge.Api.Controllers;

/// <summary>Admin quản lý 4 danh mục dùng chung toàn nền tảng (thể loại nhạc, dòng nhạc/cảm xúc,
/// phong cách không gian, loại sự kiện). Xóa bị chặn (409) nếu danh mục còn đang được show/nghệ
/// sĩ/người dùng nào tham chiếu — buộc gỡ liên kết trước, không âm thầm để lại dữ liệu mồ côi.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = Policies.RequireAdmin)]
public sealed class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

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

    // ---- Loại sự kiện ----

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

    // ---- Duyệt sự kiện ----

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
}

public sealed record UpdateMusicGenreRequest(string Name, string? NameEn);
public sealed record UpdateMoodRequest(string Name);
public sealed record UpdateVenueAtmosphereRequest(string Name);
public sealed record UpdateEventCategoryRequest(string Name, string? Description, bool IsActive);
public sealed record ReviewShowRequest(string Decision, string? ReviewNote);
