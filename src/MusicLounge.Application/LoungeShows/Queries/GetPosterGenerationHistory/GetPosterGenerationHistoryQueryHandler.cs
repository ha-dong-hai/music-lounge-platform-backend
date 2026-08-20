using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Queries.GetPosterGenerationHistory;

// "Trách nhiệm với khách hàng" — lets an Owner see exactly which of their AI poster attempts
// succeeded, which failed and why, and confirms failed ones didn't cost them a poster.
internal sealed class GetPosterGenerationHistoryQueryHandler
    : IRequestHandler<GetPosterGenerationHistoryQuery, IReadOnlyList<PosterGenerationAttemptDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetPosterGenerationHistoryQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PosterGenerationAttemptDto>> Handle(
        GetPosterGenerationHistoryQuery request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem lịch sử tạo poster của show này.");

        var attempts = await _uow.Repository<AiPosterGeneration, int>().FindAsync(
            g => g.ShowId == request.ShowId, ct);

        return attempts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new PosterGenerationAttemptDto(
                a.Id, a.Status.ToString(), a.ImageUrl, a.ErrorMessage, a.CreatedAt))
            .ToList();
    }
}
