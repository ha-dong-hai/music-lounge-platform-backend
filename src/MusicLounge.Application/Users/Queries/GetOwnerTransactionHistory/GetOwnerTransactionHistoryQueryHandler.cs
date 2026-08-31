using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetOwnerTransactionHistory;

internal sealed class GetOwnerTransactionHistoryQueryHandler
    : IRequestHandler<GetOwnerTransactionHistoryQuery, PaginatedResult<OwnerTransactionDto>>
{
    private readonly ILedgerEntryRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetOwnerTransactionHistoryQueryHandler(ILedgerEntryRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<OwnerTransactionDto>> Handle(
        GetOwnerTransactionHistoryQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        return await _repo.GetOwnerHistoryAsync(
            _currentUser.UserId, request.Type, request.From, request.To, page, size, ct);
    }
}
