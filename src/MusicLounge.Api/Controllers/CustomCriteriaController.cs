using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;
using MusicLounge.Application.CustomCriteria.Commands.SetEventCustomValues;
using MusicLounge.Application.CustomCriteria.DTOs;
using MusicLounge.Application.CustomCriteria.Queries.GetLoungeCustomCriteria;

namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/custom-criteria")]
[Authorize(Policy = Policies.RequireOwner)]
public sealed class CustomCriteriaController : ControllerBase
{
    private readonly ISender _sender;

    public CustomCriteriaController(ISender sender) => _sender = sender;

    /// <summary>Owner — thêm 1 tiêu chí phân loại tùy chỉnh cho venue mình (vd: ngôn ngữ biểu diễn,
    /// acoustic/electric, phụ thu bàn). Dùng cho AI gợi ý và hiển thị khi tạo sự kiện tại venue đó.
    /// Key phải duy nhất trong venue (409 nếu trùng).</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomCriteriaCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(id));
    }

    /// <summary>Owner — danh sách tiêu chí tùy chỉnh (đang active) của 1 venue mình sở hữu, dùng để
    /// hiển thị form khi tạo sự kiện tại venue đó.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CustomCriteriaDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByLounge(
        [FromQuery] int loungeId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeCustomCriteriaQuery(loungeId), ct);
        return Ok(ApiResponse<IReadOnlyList<CustomCriteriaDto>>.Ok(result));
    }

    /// <summary>Owner — gắn/cập nhật giá trị các tiêu chí tùy chỉnh cho 1 sự kiện (upsert theo
    /// CriteriaId). Chỉ chấp nhận tiêu chí thuộc đúng venue của sự kiện đó. Dữ liệu dùng cho AI
    /// matching nâng cao.</summary>
    [HttpPost("shows/{showId:int}/values")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetEventValues(
        int showId, [FromBody] IReadOnlyList<EventCustomValueInput> values, CancellationToken ct = default)
    {
        await _sender.Send(new SetEventCustomValuesCommand(showId, values), ct);
        return NoContent();
    }
}
