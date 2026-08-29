using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Notifications.Commands.RegisterDeviceToken;
using MusicLounge.Application.Notifications.Commands.UnregisterDeviceToken;

namespace MusicLounge.Api.Controllers;

/// <summary>W23 — FCM device token registration for push notifications.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender) => _sender = sender;

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
