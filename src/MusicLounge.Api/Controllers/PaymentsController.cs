using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Tickets.Commands.ProcessVnPayCallback;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly BusinessSettings _settings;

    public PaymentsController(ISender sender, IOptions<BusinessSettings> settings)
    {
        _sender = sender;
        _settings = settings.Value;
    }

    [HttpGet("vnpay/callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> VnPayCallback(CancellationToken ct = default)
    {
        var queryParams = Request.Query
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var success = await _sender.Send(
            new ProcessVnPayCallbackCommand(queryParams), ct);

        return success
            ? Redirect(_settings.PaymentSuccessUrl)
            : Redirect(_settings.PaymentFailedUrl);
    }

    // Register this URL (not vnpay/callback) as the order's IPN URL in the VNPay merchant portal.
    // vnpay/callback above only ever fires if the buyer's browser makes it back to this server —
    // it does not fire if they close the tab, lose connectivity, or the app is backgrounded right
    // after paying. VNPay calls this URL server-to-server, independent of the buyer's browser, and
    // expects an HTTP 200 with {RspCode, Message} in the body (never a redirect) — it reads RspCode
    // to decide whether to keep retrying, not the HTTP status code. Safe to call the exact same
    // idempotent command as the browser callback: whichever of the two arrives first processes the
    // payment, the other is a no-op confirmation.
    [HttpGet("vnpay/ipn")]
    [AllowAnonymous]
    [ProducesResponseType<VnPayIpnResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct = default)
    {
        var queryParams = Request.Query
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var success = await _sender.Send(
            new ProcessVnPayCallbackCommand(queryParams), ct);

        return Ok(success
            ? new VnPayIpnResponse("00", "Confirm Success")
            : new VnPayIpnResponse("99", "Unknown error"));
    }
}

/// <summary>VNPay's IPN response contract — VNPay parses this body, not the HTTP status, to decide
/// whether the callback was handled and whether to keep retrying.</summary>
public sealed record VnPayIpnResponse(string RspCode, string Message);
