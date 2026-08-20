using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Queries.GetVenueTourStitchAttempt;

internal sealed class GetVenueTourStitchAttemptQueryHandler
    : IRequestHandler<GetVenueTourStitchAttemptQuery, VenueTourStitchAttemptDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetVenueTourStitchAttemptQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<VenueTourStitchAttemptDto> Handle(GetVenueTourStitchAttemptQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xem venue này.");

        var attempt = await _uow.Repository<VenueTourStitchAttempt, int>().GetByIdAsync(request.AttemptId, ct);
        if (attempt is null || attempt.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(VenueTourStitchAttempt), request.AttemptId);

        return new VenueTourStitchAttemptDto(
            attempt.Id, attempt.Status.ToString(), attempt.ResultSceneId, attempt.ErrorMessage);
    }
}
