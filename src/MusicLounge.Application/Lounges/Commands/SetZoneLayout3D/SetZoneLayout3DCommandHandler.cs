using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetZoneLayout3D;

internal sealed class SetZoneLayout3DCommandHandler : IRequestHandler<SetZoneLayout3DCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetZoneLayout3DCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetZoneLayout3DCommand request, CancellationToken ct)
    {
        var zoneRepo = _uow.Repository<SeatingZone, int>();
        var zone = await zoneRepo.GetByIdAsync(request.ZoneId, ct)
            ?? throw new NotFoundException(nameof(SeatingZone), request.ZoneId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(zone.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), zone.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền quản lý khu vực chỗ ngồi cho venue này.");

        zone.Layout3DX = request.X;
        zone.Layout3DY = request.Y;
        zone.Layout3DZ = request.Z;

        zoneRepo.Update(zone);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
