using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.CreateLivestream;

public sealed record CreateLivestreamCommand(int ShowId, bool IsFree = true, bool ChatEnabled = true)
    : ICommand<int>;
