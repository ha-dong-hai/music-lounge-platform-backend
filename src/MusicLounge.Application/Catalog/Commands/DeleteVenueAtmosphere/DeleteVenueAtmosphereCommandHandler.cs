using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Catalog.Commands.DeleteVenueAtmosphere;

internal sealed class DeleteVenueAtmosphereCommandHandler : IRequestHandler<DeleteVenueAtmosphereCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public DeleteVenueAtmosphereCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(DeleteVenueAtmosphereCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<VenueAtmosphere, int>();
        var atmosphere = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(VenueAtmosphere), request.Id);

        // MusicLounge.AtmosphereId la FK truc tiep (khong phai bang join nhu 2 cai duoi) - de sot
        // neu chi kiem tra bang join, nen phai check rieng.
        var inUse = await _uow.Repository<LoungeShowAtmosphere, int>().AnyAsync(x => x.AtmosphereId == request.Id, ct)
            || await _uow.Repository<UserFavouriteAtmosphere, int>().AnyAsync(x => x.AtmosphereId == request.Id, ct)
            || await _uow.Repository<MusicLoungeEntity, int>().AnyAsync(x => x.AtmosphereId == request.Id, ct);
        if (inUse)
            throw new ConflictException($"Phong cách không gian '{atmosphere.Name}' đang được sử dụng, không thể xóa.");

        repo.Remove(atmosphere);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
