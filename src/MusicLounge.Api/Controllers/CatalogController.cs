using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Catalog.Queries.GetEventCategories;
using MusicLounge.Application.Catalog.Queries.GetMoods;
using MusicLounge.Application.Catalog.Queries.GetMusicGenres;
using MusicLounge.Application.Catalog.Queries.GetVenueAtmospheres;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

/// <summary>4 danh mục dùng chung toàn hệ thống, phục vụ form tạo sự kiện và trang onboarding.
/// Toàn bộ endpoint trong controller này công khai (không yêu cầu đăng nhập) — không có endpoint
/// nào khác cần bảo vệ nên [AllowAnonymous] đặt ở class level là an toàn ở đây.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/catalog")]
[AllowAnonymous]
public sealed class CatalogController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogController(ISender sender) => _sender = sender;

    [HttpGet("music-genres")]
    [ProducesResponseType<ApiResponse<List<CatalogItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMusicGenres(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMusicGenresQuery(), ct);
        return Ok(ApiResponse<List<CatalogItemDto>>.Ok(result));
    }

    [HttpGet("moods")]
    [ProducesResponseType<ApiResponse<List<CatalogItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMoods(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMoodsQuery(), ct);
        return Ok(ApiResponse<List<CatalogItemDto>>.Ok(result));
    }

    [HttpGet("venue-atmospheres")]
    [ProducesResponseType<ApiResponse<List<CatalogItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVenueAtmospheres(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetVenueAtmospheresQuery(), ct);
        return Ok(ApiResponse<List<CatalogItemDto>>.Ok(result));
    }

    [HttpGet("event-categories")]
    [ProducesResponseType<ApiResponse<List<CatalogItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventCategories(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetEventCategoriesQuery(), ct);
        return Ok(ApiResponse<List<CatalogItemDto>>.Ok(result));
    }
}
