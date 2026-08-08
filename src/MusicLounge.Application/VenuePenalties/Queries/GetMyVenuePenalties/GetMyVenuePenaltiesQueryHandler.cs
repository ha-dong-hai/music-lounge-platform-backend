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

        // Repository<T> generic khong ho tro Include — filter theo Lounge.OwnerId van dich duoc
        // sang JOIN o tang SQL (chi can cho WHERE), nhung sau khi materialize thi p.Lounge se null
        // (AsNoTracking, khong lazy-loading). Fetch ten lounge rieng roi join phia client, giong
        // pattern da dung o LoungeShowRepository.GetWishlistByUserAsync/GetTrendingAsync.
        var mine = await _uow.Repository<VenuePenalty, int>()
            .FindAsync(p => p.Lounge.OwnerId == _currentUser.UserId, ct);

        var ordered = mine.OrderByDescending(p => p.IssuedAt).ToList();
        var pageItems = ordered.Skip((page - 1) * size).Take(size).ToList();

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

        return new PaginatedResult<VenuePenaltyDto>(items, page, size, ordered.Count);
    }
}
