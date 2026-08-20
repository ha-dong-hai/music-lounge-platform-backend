using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.ChangeLoungeShowFormat;
using MusicLounge.Application.LoungeShows.Commands.CreateLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.EndLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.GeneratePoster;
using MusicLounge.Application.LoungeShows.Commands.PublishLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.StartLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.RateShow;
using MusicLounge.Application.LoungeShows.Commands.RescheduleLoungeShow;
using MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;
using MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;
using MusicLounge.Application.LoungeShows.Commands.SetShowCoverImage;
using MusicLounge.Application.LoungeShows.Commands.SetShowPoster;
using MusicLounge.Application.LoungeShows.Commands.SetVcpmcRoyaltyReference;
using MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.LoungeShows.Queries.GetFilterOptions;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByLounge;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByPerformer;
using MusicLounge.Application.LoungeShows.Queries.GetLoungeShowSuggestions;
using MusicLounge.Application.LoungeShows.Queries.GetPosterGenerationHistory;
using MusicLounge.Application.LoungeShows.Queries.GetPublishedLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.GetShowSeatingMap;
using MusicLounge.Application.LoungeShows.Queries.GetTrendingLoungeShows;
using MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Queries.GetShowOrders;
using MusicLounge.Domain.Enums;



namespace MusicLounge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/lounge-shows")]
public sealed class LoungeShowsController : ControllerBase
{
    private readonly ISender _sender;

    public LoungeShowsController(ISender sender) => _sender = sender;

    [HttpGet]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublished(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] LoungeShowSortBy sortBy = LoungeShowSortBy.Newest,
        [FromQuery] bool includeSoldOut = true,
        [FromQuery] bool mine = false,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetPublishedLoungeShowsQuery(page, pageSize, sortBy, includeSoldOut, mine), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpGet("suggestions")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] string q,
        [FromQuery] int limit = 8,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>.Ok([]));

        var result = await _sender.Send(new GetLoungeShowSuggestionsQuery(q, limit), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeShowSuggestionItem>>.Ok(result));
    }

    [HttpGet("filter-options")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<FilterOptionsDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetFilterOptionsQuery(), ct);
        return Ok(ApiResponse<FilterOptionsDto>.Ok(result));
    }

    [HttpGet("search")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] int[]? genreIds,
        [FromQuery] int[]? moodIds,
        [FromQuery] int[]? atmosphereIds,
        [FromQuery] int? performerId,
        [FromQuery] int? loungeId,
        [FromQuery] string? city,
        [FromQuery] string? district,
        [FromQuery] string? ward,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] LoungeShowFormat? format,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] bool includeSoldOut = true,
        [FromQuery] bool includeEnded = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] LoungeShowSortBy sortBy = LoungeShowSortBy.Newest,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new SearchLoungeShowsQuery(
            keyword, genreIds, moodIds, atmosphereIds,
            performerId, loungeId, city, district, ward,
            dateFrom, dateTo, format, minPrice, maxPrice,
            includeSoldOut, includeEnded, page, pageSize, sortBy), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpGet("trending")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrending(
        [FromQuery] int limit = 10,
        [FromQuery] string? city = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTrendingLoungeShowsQuery(limit, city), ct);
        return Ok(ApiResponse<IReadOnlyList<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<LoungeShowDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLoungeShowDetailQuery(id), ct);
        return Ok(ApiResponse<LoungeShowDetailDto>.Ok(result));
    }

    /// <summary>Bản đồ chọn khu vực cho show này — chỉ gồm zone có ít nhất 1 hạng vé thuộc show.</summary>
    [HttpGet("{id:int}/seating-map")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<SeatingMapDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeatingMap(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowSeatingMapQuery(id), ct);
        return Ok(ApiResponse<SeatingMapDto>.Ok(result));
    }

    [HttpGet("{id:int}/orders")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ShowOrderDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrders(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetShowOrdersQuery(id, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ShowOrderDto>>.Ok(result));
    }

    [HttpGet("by-performer/{performerId:int}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PerformerDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPerformer(
        int performerId,
        [FromQuery] bool includeEnded = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetLoungeShowsByPerformerQuery(performerId, includeEnded, page, pageSize), ct);
        return Ok(ApiResponse<PerformerDetailDto>.Ok(result));
    }

    [HttpPost("{id:int}/rate")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rate(
        int id, [FromBody] RateShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RateShowCommand(id, body.Score, body.Comment), ct);
        return NoContent();
    }

    [HttpGet("by-lounge/{loungeId:int}")]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<PaginatedResult<LoungeShowListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLounge(
        int loungeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetLoungeShowsByLoungeQuery(loungeId, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<LoungeShowListItemDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLoungeShowCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetDetail), new { id, version = "1.0" }, ApiResponse<int>.Ok(id));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateLoungeShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateLoungeShowCommand(
            id, body.Name, body.Description, body.ScheduledStart, body.ScheduledEnd,
            body.CategoryId, body.OfflineQuota, body.OnlineQuota), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct = default)
    {
        await _sender.Send(new PublishLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct = default)
    {
        await _sender.Send(new CancelLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reschedule")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reschedule(
        int id, [FromBody] RescheduleLoungeShowRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new RescheduleLoungeShowCommand(id, body.NewScheduledStart), ct);
        return NoContent();
    }

    // W02: gated by the Owner's subscription (HasAiPosterSnapshot) inside the handler, not by
    // policy here — RequireOwner only proves "an Owner", the actual entitlement check needs the
    // Owner's specific active subscription snapshot.
    [HttpPost("{id:int}/ai-poster")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<PosterGenerationResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GeneratePoster(
        int id, [FromBody] GeneratePosterRequest? body, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GeneratePosterCommand(id, body?.StyleHint), ct);
        return Ok(ApiResponse<PosterGenerationResultDto>.Ok(result));
    }

    [HttpGet("{id:int}/ai-poster/history")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PosterGenerationAttemptDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosterHistory(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPosterGenerationHistoryQuery(id), ct);
        return Ok(ApiResponse<IReadOnlyList<PosterGenerationAttemptDto>>.Ok(result));
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(int id, CancellationToken ct = default)
    {
        await _sender.Send(new StartLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/end")]
    [Authorize(Policy = Policies.RequireVenueOperator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> End(int id, CancellationToken ct = default)
    {
        await _sender.Send(new EndLoungeShowCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/legal-approval")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetLegalApprovalReference(
        int id, [FromBody] SetLegalApprovalReferenceRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetLegalApprovalReferenceCommand(id, body.LegalApprovalReference), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/vcpmc-royalty")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetVcpmcRoyaltyReference(
        int id, [FromBody] SetVcpmcRoyaltyReferenceRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetVcpmcRoyaltyReferenceCommand(id, body.VcpmcRoyaltyReference), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/cover-image")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCoverImage(
        int id, [FromBody] SetShowCoverImageRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetShowCoverImageCommand(id, body.ImageUrl), ct);
        return NoContent();
    }

    // Manual counterpart to POST {id}/ai-poster — for an Owner who wants to upload their own
    // poster instead of generating one, or doesn't have the AI-poster subscription tier at all.
    [HttpPut("{id:int}/poster")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPoster(
        int id, [FromBody] SetShowPosterRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetShowPosterCommand(id, body.ImageUrl), ct);
        return NoContent();
    }

    /// <summary>Tour ảo 3D / Online concert 3D: Owner chọn phát 2D (mặc định) hay 3D cho show Online/Hybrid.</summary>
    [HttpPut("{id:int}/playback-mode")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPlaybackMode(
        int id, [FromBody] SetPlaybackModeRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new SetPlaybackModeCommand(id, body.PlaybackMode), ct);
        return NoContent();
    }

    [HttpPut("{id:int}/format")]
    [Authorize(Policy = Policies.RequireOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeFormat(
        int id, [FromBody] ChangeLoungeShowFormatRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ChangeLoungeShowFormatCommand(id, body.NewFormat), ct);
        return NoContent();
    }
}

public sealed record RateShowRequest(int Score, string? Comment);

public sealed record RescheduleLoungeShowRequest(DateTimeOffset NewScheduledStart);

public sealed record GeneratePosterRequest(string? StyleHint);

public sealed record ChangeLoungeShowFormatRequest(LoungeShowFormat NewFormat);

public sealed record SetLegalApprovalReferenceRequest(string LegalApprovalReference);

public sealed record SetVcpmcRoyaltyReferenceRequest(string VcpmcRoyaltyReference);

public sealed record SetShowCoverImageRequest(string ImageUrl);

public sealed record SetShowPosterRequest(string ImageUrl);

public sealed record SetPlaybackModeRequest(string PlaybackMode);

public sealed record UpdateLoungeShowRequest(
    string Name,
    string Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    int? CategoryId,
    int? OfflineQuota,
    int? OnlineQuota);
