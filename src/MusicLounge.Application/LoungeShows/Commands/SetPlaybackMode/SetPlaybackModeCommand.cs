using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;

/// <param name="PlaybackMode">"TwoD" hoặc "ThreeD", khớp với enum LivestreamPlaybackMode.</param>
public sealed record SetPlaybackModeCommand(Guid ShowId, string PlaybackMode) : ICommand;
