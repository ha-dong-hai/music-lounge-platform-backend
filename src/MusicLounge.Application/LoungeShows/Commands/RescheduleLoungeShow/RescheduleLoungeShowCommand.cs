using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.RescheduleLoungeShow;

public sealed record RescheduleLoungeShowCommand(Guid ShowId, DateTimeOffset NewScheduledStart) : ICommand;
