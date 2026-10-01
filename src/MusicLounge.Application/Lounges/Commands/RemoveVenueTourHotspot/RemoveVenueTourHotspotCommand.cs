using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.RemoveVenueTourHotspot;

public sealed record RemoveVenueTourHotspotCommand(Guid LoungeId, Guid HotspotId) : ICommand;
