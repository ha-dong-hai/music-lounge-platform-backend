using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Catalog.Queries.GetEventCategories;
using MusicLounge.Application.Catalog.Queries.GetMoneyTerms;
using MusicLounge.Application.Catalog.Queries.GetProvinces;
using MusicLounge.Application.Catalog.Queries.GetWardsOfProvince;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Catalog.Queries.GetMoods;
using MusicLounge.Application.Catalog.Queries.GetMusicGenres;
using MusicLounge.Application.Catalog.Queries.GetVenueAtmospheres;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Api.Controllers;

/// <summary>4 danh mục dùng chung toàn hệ thống, phục vụ form tạo buổi diễn và trang onboarding.
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
    [ProducesResponseType<ApiResponse<List<MusicGenreCatalogItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMusicGenres(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMusicGenresQuery(), ct);
        return Ok(ApiResponse<List<MusicGenreCatalogItemDto>>.Ok(result));
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

    /// <summary>MLACP-521: 34 tỉnh/thành phố từ 01/7/2025 (QĐ 19/2025/QĐ-TTg). Mã là mã chính thức của Cục Thống kê.</summary>
    [HttpGet("provinces")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdministrativeProvince>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvinces(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetProvincesQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<AdministrativeProvince>>.Ok(result));
    }

    /// <summary>MLACP-625: biểu phí và điều khoản tiền ĐANG ÁP DỤNG (phí nền tảng, thuế khấu trừ, hạn hoàn tiền, cách
    /// chia tiền ủng hộ, lịch chi cho phòng trà). Web in các con số này ở bước mua vé, hộp ủng hộ và trang Điều khoản —
    /// không gõ cứng, vì Admin đổi được qua PUT /admin/system-config.</summary>
    [HttpGet("money-terms")]
    [ProducesResponseType<ApiResponse<MoneyTermsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMoneyTerms(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMoneyTermsQuery(), ct);
        return Ok(ApiResponse<MoneyTermsDto>.Ok(result));
    }

    /// <summary>MLACP-521: phường/xã của một tỉnh — không còn cấp quận/huyện ở giữa.</summary>
    [HttpGet("provinces/{provinceCode}/wards")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdministrativeWard>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWards(string provinceCode, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetWardsOfProvinceQuery(provinceCode), ct);
        return Ok(ApiResponse<IReadOnlyList<AdministrativeWard>>.Ok(result));
    }
}
