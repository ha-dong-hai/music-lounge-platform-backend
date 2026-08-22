using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.CreateMood;

public sealed record CreateMoodCommand(string Name) : ICommand<int>;
