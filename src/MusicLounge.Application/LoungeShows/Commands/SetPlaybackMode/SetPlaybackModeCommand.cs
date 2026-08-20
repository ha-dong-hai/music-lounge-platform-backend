using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;

// PlaybackMode: "TwoD" hoac "ThreeD" (chuoi, khop voi LivestreamPlaybackMode enum).
public sealed record SetPlaybackModeCommand(int ShowId, string PlaybackMode) : ICommand;
