using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.Admin.Commands.TriggerRecurringJob;
using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Admin.Queries.GetLedgerIntegrity;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Application.Refunds.Queries.GetPendingRefundRequests;
using MusicLounge.Application.Users.Commands.DeactivateUserAccount;
using MusicLounge.Application.Users.Commands.ReactivateUserAccount;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Application.Users.Queries.GetUserDetail;
using MusicLounge.Application.Users.Queries.GetUsers;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Api.Controllers;

/// <summary>W28 — financial reconciliation tools for Admin.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = Policies.RequireAdmin)]
public sealed class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    [HttpGet("ledger/integrity-check")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LedgerIntegrityIssueDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LedgerIntegrityCheck(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetLedgerIntegrityQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<LedgerIntegrityIssueDto>>.Ok(result));
    }

    /// <summary>W26 — refund requests created by CancelTicket, awaiting Admin decision.</summary>
    [HttpGet("refund-requests")]
    [ProducesResponseType<ApiResponse<PaginatedResult<RefundRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRefundRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetPendingRefundRequestsQuery(page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<RefundRequestDto>>.Ok(result));
    }

    [HttpPost("refund-requests/{id:int}/process")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProcessRefundRequest(
        int id, [FromBody] ProcessRefundRequestBody body, CancellationToken ct = default)
    {
        await _sender.Send(new ProcessRefundRequestCommand(id, body.Decision, body.ApprovedAmount), ct);
        return NoContent();
    }

    /// <summary>Ep chay ngay 1 recurring job da dang ky (van hanh/kiem tra), khong doi lich Cron.</summary>
    [HttpPost("jobs/{jobId}/trigger")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerRecurringJob(string jobId, CancellationToken ct = default)
    {
        await _sender.Send(new TriggerRecurringJobCommand(jobId), ct);
        return NoContent();
    }

    [HttpGet("users")]
    [ProducesResponseType<ApiResponse<PaginatedResult<UserAdminDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? searchText,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetUsersQuery(searchText, role, isActive, page, pageSize), ct);
        return Ok(ApiResponse<PaginatedResult<UserAdminDto>>.Ok(result));
    }

    [HttpGet("users/{id:int}")]
    [ProducesResponseType<ApiResponse<UserAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(int id, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetUserDetailQuery(id), ct);
        return Ok(ApiResponse<UserAdminDto>.Ok(result));
    }

    [HttpPost("users/{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUserAccount(int id, CancellationToken ct = default)
    {
        await _sender.Send(new DeactivateUserAccountCommand(id), ct);
        return NoContent();
    }

    [HttpPost("users/{id:int}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateUserAccount(int id, CancellationToken ct = default)
    {
        await _sender.Send(new ReactivateUserAccountCommand(id), ct);
        return NoContent();
    }
}

public sealed record ProcessRefundRequestBody(string Decision, decimal? ApprovedAmount);
