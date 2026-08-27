using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Users.Commands.UpdateAiPreferences;

namespace MusicLounge.Api.Controllers;

// Luu y: cac task sau (profile, KYC, DSAR, xac thuc SDT...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class MeController : ControllerBase
{
    private readonly ISender _sender;

    public MeController(ISender sender) => _sender = sender;

    /// <summary>Lưu sở thích âm nhạc (thể loại/tâm trạng/không gian) làm đầu vào cho AI gợi ý — dùng
    /// cả cho onboarding lần đầu sau đăng ký lẫn cập nhật lại trong Cài đặt sau này (ghi đè toàn bộ
    /// danh sách theo request, không phải thêm dần). Mọi field có thể để rỗng — không có bước nào
    /// khác trong hệ thống bắt buộc phải hoàn thành onboarding này mới dùng được ứng dụng.</summary>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateAiPreferencesCommand command,
        CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }
}
