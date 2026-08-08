using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.Commands.DeactivateMyAccount;
using MusicLounge.Application.Users.Commands.SubmitCitizenCard;
using MusicLounge.Application.Users.Commands.UpdateAiPreferences;
using MusicLounge.Application.Users.Commands.UpdateMyProfile;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Application.Users.Queries.GetMyCitizenCardImage;
using MusicLounge.Application.Users.Queries.GetMyEarnings;
using MusicLounge.Application.Users.Queries.GetMyProfile;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/me")]
[Authorize(Policy = Policies.RequireAuthenticated)]
public sealed class MeController : ControllerBase
{
    private readonly ISender _sender;

    public MeController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<UserProfileDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyProfileQuery(), ct);
        return Ok(ApiResponse<UserProfileDto>.Ok(result));
    }

    [HttpGet("earnings")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<EarningsSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEarnings(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyEarningsQuery(), ct);
        return Ok(ApiResponse<EarningsSummaryDto>.Ok(result));
    }

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

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateMyProfileCommand command,
        CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>KYC — nộp số CCCD/CMND + ảnh mặt trước/sau (lấy URL từ POST /uploads/images trước đó).</summary>
    [HttpPost("citizen-card")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitCitizenCard(
        [FromBody] SubmitCitizenCardCommand command,
        CancellationToken ct = default)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Xem lại ảnh CCCD/CMND đã nộp — chỉ chính chủ. File nằm ngoài wwwroot, không đoán URL truy cập trực tiếp được.</summary>
    [HttpGet("citizen-card/{side}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyCitizenCardImage(string side, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyCitizenCardImageQuery(side), ct);
        return File(result.Content, result.ContentType);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateMyAccount(CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateMyAccountCommand(), ct);
        return NoContent();
    }
}
