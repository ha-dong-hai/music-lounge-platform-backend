using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.CreateLivestream;

public sealed record CreateLivestreamCommand(int ShowId) : ICommand<int>;
