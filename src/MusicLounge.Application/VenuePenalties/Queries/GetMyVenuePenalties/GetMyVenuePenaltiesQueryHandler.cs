using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.VenuePenalties.DTOs;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties.Queries.GetMyVenuePenalties;

internal sealed class GetMyVenuePenaltiesQueryHandler
    : IRequestHandler<GetMyVenuePenaltiesQuery, PaginatedResult<VenuePenaltyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMyVenuePenaltiesQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<VenuePenaltyDto>> Handle(
        GetMyVenuePenaltiesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        // p.Lounge.OwnerId is only used to filter (translates to a SQL JOIN), never to read
        // p.Lounge itself — GetPagedAsync orders/counts/pages server-side instead of pulling this
        // owner's entire penalty history into memory just to throw most of it away per page. Lounge
        // names are still fetched separately below since Repository<T> has no Include.
        //
        // Order by Id, not IssuedAt — ordering by a DateTimeOffset column doesn't translate under
        // the SQLite provider used in tests (same limitation documented throughout this codebase).
        // A penalty's Id is assigned at insert time and IssuedAt is set to UtcNow at that same
        // moment, so Id order and IssuedAt order are always identical here (penalties are never
        // backdated), and Id keeps pagination server-side on both SQLite and SQL Server.
        var (pageItems, totalCount) = await _uow.Repository<VenuePenalty, int>()
            .GetPagedAsync(p => p.Lounge.OwnerId == _currentUser.UserId, p => p.Id, page, size, ct);

        var loungeIds = pageItems.Select(p => p.LoungeId).Distinct().ToList();
        var lounges = await _uow.Repository<MusicLoungeEntity, int>()
            .FindAsync(l => loungeIds.Contains(l.Id), ct);
        var loungeNames = lounges.ToDictionary(l => l.Id, l => l.Name);

        var items = pageItems
            .Select(p => new VenuePenaltyDto(
                p.Id, p.LoungeId, loungeNames.GetValueOrDefault(p.LoungeId, string.Empty),
                p.PenaltyType, p.Reason, p.EvidenceRef,
                p.IssuedAt, p.EffectiveAt, p.SuspensionDays, p.SuspensionEnd, p.Status,
                p.AppealDeadline, p.AppealedAt, p.AppealReason, p.AppealResult, p.ReviewedAt))
            .ToList();

        return new PaginatedResult<VenuePenaltyDto>(items, page, size, totalCount);
    }
}
