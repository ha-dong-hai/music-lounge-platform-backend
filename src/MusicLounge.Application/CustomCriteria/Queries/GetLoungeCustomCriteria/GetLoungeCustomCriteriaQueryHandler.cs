using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.CustomCriteria.DTOs;
using MusicLounge.Domain.Exceptions;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.CustomCriteria.Queries.GetLoungeCustomCriteria;

internal sealed class GetLoungeCustomCriteriaQueryHandler
    : IRequestHandler<GetLoungeCustomCriteriaQuery, IReadOnlyList<CustomCriteriaDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeCustomCriteriaQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CustomCriteriaDto>> Handle(
        GetLoungeCustomCriteriaQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem tiêu chí của venue này.");

        var criteria = await _uow.Repository<CustomCriteriaEntity, int>().FindAsync(
            c => c.LoungeId == request.LoungeId && c.IsActive, ct);

        return criteria
            .OrderBy(c => c.Name)
            .Select(c => new CustomCriteriaDto(
                c.Id, c.LoungeId, c.Name, c.Key, c.DataType, c.Options, c.IsActive, c.CreatedAt))
            .ToList();
    }
}
