using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Api.Swagger;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.Commands.CreateComplaint;
using MusicLounge.Application.Complaints.Commands.ResolveComplaint;
using MusicLounge.Application.Complaints.DTOs;
using MusicLounge.Application.Complaints.Queries.GetMyComplaints;
using MusicLounge.Application.Complaints.Queries.GetPendingComplaints;

namespace MusicLounge.Api.Controllers;

/// <summary>W30 — NĐ 85/2021 khiếu nại & kháng cáo.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/complaints")]
public sealed class ComplaintsController : ControllerBase
{
    private readonly ISender _sender;

    public ComplaintsController(ISender sender) => _sender = sender;

    [HttpPost]
    [AllowAnonymous]
    [SwaggerOptionalAuth]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateComplaintCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        // Khong co GET /complaints/{id} don le — CreatedAtAction(nameof(GetMy)) truoc day khong
        // truyen id nen Location tro ve nguyen list, khong dinh danh duoc complaint vua tao.
        return StatusCode(StatusCodes.Status201Created, ApiResponse<int>.Ok(id));
    }

    [HttpGet("my")]
    [Authorize(Policy = Policies.RequireAuthenticated)]
    [ProducesResponseType<ApiResponse<PaginatedResult<ComplaintDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetMyComplaintsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ComplaintDto>>.Ok(result));
    }
}

/// <summary>Admin — quản lý khiếu nại.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/complaints")]
[Authorize(Policy = Policies.RequireAdmin)]
public sealed class AdminComplaintsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminComplaintsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<PaginatedResult<ComplaintDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingComplaintsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<ComplaintDto>>.Ok(result));
    }

    [HttpPost("{id:int}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        int id, [FromBody] ResolveComplaintRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new ResolveComplaintCommand(id, body.Status, body.Resolution, body.ResolvedAction), ct);
        return NoContent();
    }
}

public sealed record ResolveComplaintRequest(string Status, string? Resolution, string? ResolvedAction);
