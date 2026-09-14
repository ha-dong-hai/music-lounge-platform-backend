using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Donations.Commands.ProcessDonationPayment;
using MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;
using MusicLounge.Application.Subscriptions.Commands.ProcessSubscriptionPayment;
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

        var outcome = await _sender.Send(
            new ProcessVnPayCallbackCommand(queryParams), ct);

        return Redirect(VnPayIpnProtocol.BuyerLandingUrl(outcome, _settings));
    }

    // Register this URL (not vnpay/callback) as the IPN URL of the VNPay terminal — it is the ONE IPN URL for every
    // online payment flow (tickets, donations, F&B, subscriptions), see MLACP-394 below.
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

        // MLACP-394: VNPay gan IPN URL theo terminal (vnp_TmnCode), khong theo giao dich, ma ca 4 luong dung chung mot
        // TmnCode — nen day la URL IPN duy nhat. Re nhanh theo tien to vnp_TxnRef he thong tu sinh (VnPayOrderRefs); moi
        // command van tu kiem chu ky va tu tra ket qua y nhu khi goi qua endpoint rieng cua no.
        queryParams.TryGetValue("vnp_TxnRef", out var txnRef);
        var outcome = VnPayOrderRefs.FlowOf(txnRef) switch
        {
            VnPayFlow.Donation => await _sender.Send(new ProcessDonationPaymentCommand(queryParams), ct),
            VnPayFlow.FnbOrder => await _sender.Send(new ProcessFnbOrderPaymentCommand(queryParams), ct),
            VnPayFlow.Subscription => await _sender.Send(new ProcessSubscriptionPaymentCommand(queryParams), ct),
            _ => await _sender.Send(new ProcessVnPayCallbackCommand(queryParams), ct)
        };

        // MLACP-334. Truoc day moi thu khong phai thanh cong deu tra 99 "Unknown error", ma
        // theo tai lieu VNPay la ma RETRY DUOC — nen mot callback trung lap binh thuong hay mot
        // chu ky gia mao cung khien VNPay goi lai du 10 lan trong ~50 phut. Bang map o VnPayIpnProtocol.
        var (rspCode, message) = VnPayIpnProtocol.ResponseFor(outcome);
        return Ok(new VnPayIpnResponse(rspCode, message));
    }
}

/// <summary>VNPay's IPN response contract — VNPay parses this body, not the HTTP status, to decide
/// whether the callback was handled and whether to keep retrying.</summary>
public sealed record VnPayIpnResponse(string RspCode, string Message);
