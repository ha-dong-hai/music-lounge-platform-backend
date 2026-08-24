using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.Commands.CreatePerformer;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Application.Performers.Queries.GetPerformers;

namespace MusicLounge.Api.Controllers;

// Nghe si la danh muc dung chung cho moi Owner (khong gioi han theo 1 phong tra) - GetAll dung de
// tim goi y truoc khi quyet dinh tao moi hay dung lai nghe si co san.
// Luu y: cac task sau (sua ho so, social links, xem chi tiet...) se chi them method vao file nay.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/performers")]
[Authorize(Policy = Policies.RequireOwner)]
public sealed class PerformersController : ControllerBase
{
    private readonly ISender _sender;

    public PerformersController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<PerformerDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPerformersQuery(search, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<PerformerDto>>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePerformerCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return Ok(ApiResponse<int>.Ok(id));
    }
}
