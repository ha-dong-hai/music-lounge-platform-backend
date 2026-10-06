using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.Commands.ReviewLivestream;
using MusicLounge.Application.Moderations.Commands.ReviewLoungeMedia;
using MusicLounge.Application.Moderations.Commands.ReviewShow;
using MusicLounge.Application.Moderations.Commands.ReviewTicketTier;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Application.Moderations.Queries.GetPendingModerations;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/moderations")]
[Microsoft.AspNetCore.Authorization.Authorize(Policy = Policies.RequireAdmin)]
public sealed class EventModerationsController : ControllerBase
{
    private readonly ISender _sender;

    public EventModerationsController(ISender sender) => _sender = sender;

    [HttpGet("pending")]
    [ProducesResponseType<ApiResponse<PaginatedResult<EventModerationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] string? targetType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? targetId = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingModerationsQuery(targetType, page, pageSize, targetId), ct);
        return Ok(ApiResponse<PaginatedResult<EventModerationDto>>.Ok(result));
    }

    [HttpPost("livestreams/{livestreamId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewLivestream(
        Guid livestreamId,
        [FromBody] ReviewLivestreamRequest body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ReviewLivestreamCommand(livestreamId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    [HttpPost("shows/{showId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewShow(
        Guid showId,
        [FromBody] ReviewLivestreamRequest body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ReviewShowCommand(showId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    /// <summary>MLACP-388: duyệt hạng vé livestream được thêm sau khi buổi diễn đã đăng. Approved thì giá của hạng vé
    /// mở bán; Rejected (bắt buộc ghi lý do) thì hạng vé không bán. Chủ phòng trà được báo kết quả. Chỉ duyệt được một lần
    /// (409), và chỉ khi buổi diễn chưa kết thúc hay bị huỷ.</summary>
    [HttpPost("ticket-tiers/{tierId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewTicketTier(
        Guid tierId,
        [FromBody] ReviewLivestreamRequest body,
        CancellationToken ct = default)
    {
        await _sender.Send(new ReviewTicketTierCommand(tierId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    /// <summary>MLACP-692: duyệt ảnh thư viện bị AI gắn cờ. Approved giữ ảnh; Rejected (bắt buộc lý do) gỡ ảnh và báo chủ
    /// phòng trà. Chỉ duyệt được một lần (409).</summary>
    [HttpPost("gallery-images/{imageId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewGalleryImage(
        Guid imageId, [FromBody] ReviewLivestreamRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ReviewLoungeMediaCommand("GalleryImage", imageId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }

    /// <summary>MLACP-692: duyệt cảnh tour 360 bị AI gắn cờ. Approved giữ cảnh; Rejected (bắt buộc lý do) gỡ cảnh và báo
    /// chủ phòng trà. Chỉ duyệt được một lần (409).</summary>
    [HttpPost("tour-scenes/{sceneId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewTourScene(
        Guid sceneId, [FromBody] ReviewLivestreamRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ReviewLoungeMediaCommand("TourScene", sceneId, body.Decision, body.ReviewNote), ct);
        return NoContent();
    }
}

public sealed record ReviewLivestreamRequest(string Decision, string? ReviewNote);
