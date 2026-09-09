using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Mutes.Commands.MuteLounge;
using MusicLounge.Application.Mutes.Commands.UnmuteLounge;
using MusicLounge.Application.Mutes.DTOs;
using MusicLounge.Application.Mutes.Queries.GetMyMutedLounges;

namespace MusicLounge.Api.Controllers;

/// <summary>
/// MLACP-330. Chiều ngược lại của <see cref="FollowsController"/>: nói với hệ gợi ý rằng bạn không
/// quan tâm tới một phòng trà.
///
/// Trước đây hệ gợi ý không có bất kỳ tín hiệu tiêu cực nào — mọi thứ nó học được đều là tích cực,
/// nên một suy đoán sai không có đường nào sửa. Và sở thích tiêu cực thì không suy ra ngầm được:
/// người dùng chỉ bấm vào, chỉ xem, chỉ mua thứ họ thấy thú vị, nên thứ họ không thích không để lại
/// dấu vết nào cả. Không hỏi thì không bao giờ biết.
///
/// <b>Tắt tiếng chỉ ảnh hưởng GỢI Ý, không ảnh hưởng tìm kiếm.</b> Nó chặn phòng trà khỏi những gì
/// hệ thống chủ động đẩy tới; người dùng vẫn tìm ra được nếu tự đi tìm. Cắt cả đường tìm kiếm là
/// quyết hộ họ một chuyện họ chưa hề yêu cầu.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mutes")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class MutesController : ControllerBase
{
    private readonly ISender _sender;

    public MutesController(ISender sender) => _sender = sender;

    /// <summary>Những phòng trà bạn đã tắt tiếng, mới nhất trước — để xem lại và gỡ.</summary>
    [HttpGet("lounges")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MutedLoungeDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMutedLounges(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyMutedLoungesQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<MutedLoungeDto>>.Ok(result));
    }

    /// <summary>Tắt tiếng một phòng trà. Nếu đang theo dõi thì việc theo dõi được gỡ luôn.</summary>
    [HttpPost("lounges/{loungeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Mute(int loungeId, CancellationToken ct = default)
    {
        await _sender.Send(new MuteLoungeCommand(loungeId), ct);
        return NoContent();
    }

    /// <summary>Bỏ tắt tiếng.</summary>
    [HttpDelete("lounges/{loungeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unmute(int loungeId, CancellationToken ct = default)
    {
        await _sender.Send(new UnmuteLoungeCommand(loungeId), ct);
        return NoContent();
    }
}
