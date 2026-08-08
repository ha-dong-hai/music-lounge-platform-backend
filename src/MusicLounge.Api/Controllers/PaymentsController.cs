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
}
