using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.UpdateVenueAtmosphere;

public sealed record UpdateVenueAtmosphereCommand(Guid Id, string Name) : ICommand;
