using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Subscriptions.Commands.CreateSubscriptionPackage;
using MusicLounge.Application.Subscriptions.Commands.ProcessSubscriptionPayment;
using MusicLounge.Application.Subscriptions.Commands.RenewSubscription;
using MusicLounge.Application.Subscriptions.Commands.SubscribeToPackage;
using MusicLounge.Application.Subscriptions.Commands.UpdateSubscriptionPackage;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Application.Subscriptions.Queries.GetMySubscription;
using MusicLounge.Application.Subscriptions.Queries.GetSubscriptionPackages;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subscriptions")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly BusinessSettings _settings;

    public SubscriptionsController(ISender sender, IOptions<BusinessSettings> settings)
    {
        _sender = sender;
        _settings = settings.Value;
    }

    [HttpGet("packages")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SubscriptionPackageDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPackages(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetSubscriptionPackagesQuery(activeOnly), ct);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionPackageDto>>.Ok(result));
    }

    [HttpPost("packages")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePackage(
        [FromBody] CreateSubscriptionPackageCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetPackages), new { version = "1.0" }, ApiResponse<int>.Ok(id));
    }

    [HttpPut("packages/{id:int}")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePackage(
        int id, [FromBody] UpdateSubscriptionPackageRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateSubscriptionPackageCommand(
            id, body.Description, body.Price, body.MaxTicketsPerEvent, body.HasAiPoster,
            body.MaxAiPostersPerMonth, body.IsActive), ct);
        return NoContent();
    }

    [HttpPost("subscribe")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<SubscriptionPaymentInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Subscribe(
        [FromBody] SubscribeToPackageRequest body, CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _sender.Send(new SubscribeToPackageCommand(body.PackageId, ip), ct);
        // Handler da persist 1 ban ghi Payment (Pending) truoc khi tra ve — dung 201 giong
        // Donations.Create/Tickets.Purchase (cung ban chat "khoi tao thanh toan").
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SubscriptionPaymentInitiationDto>.Ok(result));
    }

    // Convenience counterpart to Subscribe — re-uses whichever package the Owner was last on
    // instead of making them re-pick from the catalog. Still a normal VNPay checkout (one OTP tap):
    // VNPay's token API has no silent merchant-initiated charge, so this can't be truly automatic —
    // see RenewSubscriptionCommand's header comment.
    [HttpPost("renew")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<SubscriptionPaymentInitiationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Renew(CancellationToken ct = default)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _sender.Send(new RenewSubscriptionCommand(ip), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SubscriptionPaymentInitiationDto>.Ok(result));
    }

    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct = default)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var success = await _sender.Send(new ProcessSubscriptionPaymentCommand(queryParams), ct);
        return success
            ? Redirect(_settings.PaymentSuccessUrl)
            : Redirect(_settings.PaymentFailedUrl);
    }

    // Register this URL (not vnpay-return) as the order's IPN URL in the VNPay merchant portal —
    // see PaymentsController.VnPayIpn for why the browser-redirect endpoint above isn't a reliable
    // substitute for it.
    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    [ProducesResponseType<VnPayIpnResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct = default)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        var success = await _sender.Send(new ProcessSubscriptionPaymentCommand(queryParams), ct);
        return Ok(success
            ? new VnPayIpnResponse("00", "Confirm Success")
            : new VnPayIpnResponse("99", "Unknown error"));
    }

    [HttpGet("my")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<MySubscriptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMy(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMySubscriptionQuery(), ct);
        return Ok(ApiResponse<MySubscriptionDto?>.Ok(result));
    }
}

public sealed record UpdateSubscriptionPackageRequest(
    string? Description, decimal Price, int MaxTicketsPerEvent, bool HasAiPoster,
    int MaxAiPostersPerMonth, bool IsActive);

public sealed record SubscribeToPackageRequest(int PackageId);
