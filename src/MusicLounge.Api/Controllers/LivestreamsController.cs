using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Livestreams.Commands.CreateLivestream;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Application.Livestreams.Queries.GetLivestreamCredentials;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (bat dau/ket thuc stream, chat, PPV, terminate...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/livestreams")]
public sealed class LivestreamsController : ControllerBase
{
    private readonly ISender _sender;

    public LivestreamsController(ISender sender) => _sender = sender;

    /// <summary>Owner/Staff của venue tạo phiên livestream cho 1 show — mỗi show chỉ có đúng 1
    /// livestream (409 nếu đã tồn tại). Hệ thống tự sinh stream key bí mật qua provider đang active
    /// (Mux/Agora/Cloudflare); lấy lại stream key sau này qua GET /{id}/credentials.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateLivestreamCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Created($"api/v1/livestreams/{id}", ApiResponse<int>.Ok(id));
    }

    /// <summary>Staff/Admin của venue xem RTMP URL + Stream Key để cắm OBS. Không lộ ra viewer.</summary>
    [HttpGet("{id:int}/credentials")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ApiResponse<LivestreamCredentialsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredentials(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLivestreamCredentialsQuery(id), ct);
        return Ok(ApiResponse<LivestreamCredentialsDto>.Ok(result));
    }
}
