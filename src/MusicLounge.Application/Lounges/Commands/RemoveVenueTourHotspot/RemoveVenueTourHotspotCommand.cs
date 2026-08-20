using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.RemoveVenueTourHotspot;

public sealed record RemoveVenueTourHotspotCommand(int LoungeId, int HotspotId) : ICommand;
