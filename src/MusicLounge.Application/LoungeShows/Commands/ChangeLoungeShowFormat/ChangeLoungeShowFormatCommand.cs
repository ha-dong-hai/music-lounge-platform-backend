using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Commands.ChangeLoungeShowFormat;

public sealed record ChangeLoungeShowFormatCommand(int ShowId, LoungeShowFormat NewFormat) : ICommand;
