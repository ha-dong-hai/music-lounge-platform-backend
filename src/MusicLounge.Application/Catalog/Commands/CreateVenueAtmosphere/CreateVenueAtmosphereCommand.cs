using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.CreateVenueAtmosphere;

public sealed record CreateVenueAtmosphereCommand(string Name) : ICommand<int>;
