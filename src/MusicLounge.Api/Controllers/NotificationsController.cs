using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.Commands.MarkAllNotificationsRead;
using MusicLounge.Application.Notifications.Commands.MarkNotificationRead;
using MusicLounge.Application.Notifications.Commands.RegisterDeviceToken;
using MusicLounge.Application.Notifications.Commands.UnregisterDeviceToken;
using MusicLounge.Application.Notifications.DTOs;
using MusicLounge.Application.Notifications.Queries.GetMyNotifications;
using MusicLounge.Application.Notifications.Queries.GetUnreadNotificationCount;

namespace MusicLounge.Api.Controllers;

/// <summary>W23 — in-app notification inbox + FCM device token registration.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<NotificationDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyNotificationsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<NotificationDto>>.Ok(result));
    }

    /// <summary>Số badge thông báo chưa đọc, tách riêng khỏi <see cref="GetMy"/> để client
    /// (ví dụ badge trên icon chuông) không phải tải cả trang danh sách chỉ để lấy 1 con số.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var count = await _sender.Send(new GetUnreadNotificationCountQuery(), ct);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct = default)
    {
        await _sender.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        await _sender.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }

    /// <summary>App calls this on launch/login with the current FCM registration token.</summary>
    [HttpPost("devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RegisterDeviceTokenCommand(body.Token, body.Platform), ct);
        return NoContent();
    }

    /// <summary>App calls this on logout so the outgoing session stops receiving push.</summary>
    [HttpDelete("devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnregisterDevice(
        [FromBody] UnregisterDeviceTokenRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UnregisterDeviceTokenCommand(body.Token), ct);
        return NoContent();
    }
}

public sealed record RegisterDeviceTokenRequest(string Token, string? Platform = null);

public sealed record UnregisterDeviceTokenRequest(string Token);
