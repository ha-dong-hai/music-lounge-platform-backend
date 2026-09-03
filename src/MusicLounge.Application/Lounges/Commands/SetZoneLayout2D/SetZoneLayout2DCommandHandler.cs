using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetZoneLayout2D;

internal sealed class SetZoneLayout2DCommandHandler : IRequestHandler<SetZoneLayout2DCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetZoneLayout2DCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetZoneLayout2DCommand request, CancellationToken ct)
    {
        var zoneRepo = _uow.Repository<SeatingZone, int>();
        var zone = await zoneRepo.GetByIdAsync(request.ZoneId, ct)
            ?? throw new NotFoundException(nameof(SeatingZone), request.ZoneId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(zone.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), zone.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền quản lý khu vực chỗ ngồi cho venue này.");

        zone.Layout2DX = request.X;
        zone.Layout2DY = request.Y;
        zone.Layout2DWidth = request.Width;
        zone.Layout2DHeight = request.Height;
        zone.Layout2DRotationDeg = request.RotationDeg;
        zone.LayoutColor = request.Color;

        zoneRepo.Update(zone);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
