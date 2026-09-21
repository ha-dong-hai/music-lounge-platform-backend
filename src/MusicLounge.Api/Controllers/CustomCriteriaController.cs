using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;
using MusicLounge.Application.CustomCriteria.Commands.SetEventCustomValues;
using MusicLounge.Application.CustomCriteria.Commands.UpdateCustomCriteria;
using MusicLounge.Application.CustomCriteria.DTOs;
using MusicLounge.Application.CustomCriteria.Queries.GetEventCustomValues;
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
    /// acoustic/electric, phụ thu bàn). Dùng cho AI gợi ý và hiển thị khi tạo buổi diễn tại venue đó.
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
    /// hiển thị form khi tạo buổi diễn tại venue đó.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CustomCriteriaDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByLounge(
        [FromQuery] int loungeId, [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeCustomCriteriaQuery(loungeId, includeInactive), ct);
        return Ok(ApiResponse<IReadOnlyList<CustomCriteriaDto>>.Ok(result));
    }

    /// <summary>Owner — sửa tên hiển thị của 1 tiêu chí, và bật/tắt việc dùng nó.
    ///
    /// <para>Trước đây tiêu chí tạo xong là vĩnh viễn: gõ sai tên thì cái tên sai ở lại trên màn hình sửa
    /// của mọi buổi diễn, không cách nào chữa. Tắt (IsActive = false) thì tiêu chí không còn hiện khi
    /// dựng buổi diễn mới, nhưng giá trị đã gắn cho các buổi diễn cũ GIỮ NGUYÊN — muốn xem lại hoặc bật
    /// lại thì gọi GET với includeInactive=true.</para>
    ///
    /// <para>Key, DataType và Options KHÔNG sửa được ở đây: đổi chúng là làm sai kiểu hoặc làm lạc toàn
    /// bộ giá trị đã gắn từ trước.</para></summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateCustomCriteriaCommand command, CancellationToken ct = default)
    {
        await _sender.Send(command with { Id = id }, ct);
        return NoContent();
    }

    /// <summary>Owner — gắn/cập nhật giá trị các tiêu chí tùy chỉnh cho 1 buổi diễn (upsert theo
    /// CriteriaId). Chỉ chấp nhận tiêu chí thuộc đúng venue của buổi diễn đó. Dữ liệu dùng cho AI
    /// matching nâng cao.</summary>
    /// <summary>
    /// Giá trị tiêu chí riêng đang gắn cho một buổi hòa nhạc.
    ///
    /// <para>Thao tác ghi bên dưới THAY THẾ TOÀN BỘ danh sách, nhưng trước đây không có đường nào đọc giá
    /// trị đang gắn — màn hình sửa không hiện được cái gì đang có, nên mỗi lần lưu là phải nhập lại từ
    /// đầu, quên một tiêu chí là mất tiêu chí đó.</para>
    /// </summary>
    [HttpGet("shows/{showId:int}/values")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<EventCustomValueDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventValues(int showId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetEventCustomValuesQuery(showId), ct);
        return Ok(ApiResponse<IReadOnlyList<EventCustomValueDto>>.Ok(result));
    }

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
