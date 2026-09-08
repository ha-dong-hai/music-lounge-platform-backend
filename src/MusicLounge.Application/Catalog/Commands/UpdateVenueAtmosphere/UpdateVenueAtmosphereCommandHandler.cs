using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.UpdateVenueAtmosphere;

internal sealed class UpdateVenueAtmosphereCommandHandler : IRequestHandler<UpdateVenueAtmosphereCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public UpdateVenueAtmosphereCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(UpdateVenueAtmosphereCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<VenueAtmosphere, int>();
        var atmosphere = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(VenueAtmosphere), request.Id);

        var nameTaken = await repo.AnyAsync(a => a.Id != request.Id && a.Name == request.Name, ct);
        if (nameTaken)
            throw new ConflictException($"Phong cách không gian '{request.Name}' đã tồn tại.");

        atmosphere.Name = request.Name;
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
