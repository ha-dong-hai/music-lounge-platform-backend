using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.DeleteVenueAtmosphere;

public sealed record DeleteVenueAtmosphereCommand(Guid Id) : ICommand;
