using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.DeletePerformance;

public sealed record DeletePerformanceCommand(Guid PerformanceId) : ICommand;
