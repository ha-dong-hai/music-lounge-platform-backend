using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.CreateVenueAtmosphere;

internal sealed class CreateVenueAtmosphereCommandHandler
    : IRequestHandler<CreateVenueAtmosphereCommand, int>
{
    private readonly IUnitOfWork _uow;

    public CreateVenueAtmosphereCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<int> Handle(CreateVenueAtmosphereCommand request, CancellationToken ct)
    {
        var nameExists = await _uow.Repository<VenueAtmosphere, int>()
            .AnyAsync(a => a.Name == request.Name, ct);
        if (nameExists)
            throw new ConflictException($"Phong cách không gian '{request.Name}' đã tồn tại.");

        var atmosphere = new VenueAtmosphere { Name = request.Name };
        _uow.Repository<VenueAtmosphere, int>().Add(atmosphere);
        await _uow.SaveChangesAsync(ct);

        return atmosphere.Id;
    }
}
