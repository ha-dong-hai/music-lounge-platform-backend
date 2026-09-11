using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.Commands.RespondToPerformerConfirmation;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Application.Performers.Queries.LookupPerformerConfirmation;

namespace MusicLounge.Api.Controllers;

/// <summary>
/// MLACP-364 — trang nghệ sĩ mở từ liên kết trong email. Nghệ sĩ không có tài khoản, nên không cần
/// đăng nhập: token trong liên kết là thứ cho phép đúng một hành động.
///
/// <para>Token đi trong <b>body</b>, không trong đường dẫn: đường dẫn request được ghi vào log, còn một
/// token nằm trong log là một liên kết ai đọc được log cũng dùng được.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/performer-confirmations")]
[AllowAnonymous]
public sealed class PerformerConfirmationsController : ControllerBase
{
    private readonly ISender _sender;

    public PerformerConfirmationsController(ISender sender) => _sender = sender;

    /// <summary>Nghệ sĩ đang được hỏi xác nhận điều gì: tài khoản (số đã che) hoặc khoản tiền đã báo chuyển.</summary>
    [HttpPost("lookup")]
    [ProducesResponseType<ApiResponse<PerformerConfirmationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Lookup(
        [FromBody] PerformerConfirmationLookupRequest body, CancellationToken ct = default)
    {
        var result = await _sender.Send(new LookupPerformerConfirmationQuery(body.Token), ct);
        return Ok(ApiResponse<PerformerConfirmationDto>.Ok(result));
    }

    /// <summary>Nghệ sĩ xác nhận (Confirm) hoặc báo sai (Dispute). Bắt buộc đồng ý xử lý dữ liệu;
    /// liên kết dùng một lần, trong thời hạn.</summary>
    [HttpPost("respond")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Respond(
        [FromBody] RespondToPerformerConfirmationCommand command, CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }
}

public sealed record PerformerConfirmationLookupRequest(string Token);
