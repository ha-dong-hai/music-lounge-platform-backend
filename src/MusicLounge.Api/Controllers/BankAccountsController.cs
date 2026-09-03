using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Authorization;
using MusicLounge.Application.BankAccounts.Commands.CreateBankAccount;
using MusicLounge.Application.BankAccounts.Commands.UpdateBankAccount;
using MusicLounge.Application.BankAccounts.DTOs;
using MusicLounge.Application.BankAccounts.Queries.GetBankAccounts;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Api.Controllers;

// Owner registers/updates payout-destination bank accounts for their own venue (BankAccountOwnerType.Lounge)
// or for a Performer they created (BankAccountOwnerType.Performer, §6.12 edit-rights) — this is the
// registration half of Settlement.BankAccountId/Donation.BankAccountId.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/bank-accounts")]
[Authorize(Policy = Policies.RequireOwner)]
public sealed class BankAccountsController : ControllerBase
{
    private readonly ISender _sender;

    public BankAccountsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<BankAccountDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(
        [FromQuery] BankAccountOwnerType ownerType, [FromQuery] int ownerId, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetBankAccountsQuery(ownerType, ownerId), ct);
        return Ok(ApiResponse<IReadOnlyList<BankAccountDto>>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankAccountCommand command, CancellationToken ct = default)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(
            nameof(GetAll), new { ownerType = command.OwnerType, ownerId = command.OwnerId, version = "1.0" },
            ApiResponse<int>.Ok(id));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateBankAccountRequest body, CancellationToken ct = default)
    {
        await _sender.Send(new UpdateBankAccountCommand(
            id, body.BankName, body.AccountNumber, body.AccountHolder, body.IsDefault), ct);
        return NoContent();
    }
}

public sealed record UpdateBankAccountRequest(
    string BankName, string AccountNumber, string AccountHolder, bool IsDefault);
