using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.Commands.MarkAllNotificationsRead;
using MusicLounge.Application.Notifications.Commands.MarkNotificationRead;
using MusicLounge.Application.Notifications.DTOs;
using MusicLounge.Application.Notifications.Queries.GetMyNotifications;

namespace MusicLounge.Api.Controllers;

/// <summary>W23 — in-app notification inbox.</summary>
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
}
