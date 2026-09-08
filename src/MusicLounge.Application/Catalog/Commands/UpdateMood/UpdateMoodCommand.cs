using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.UpdateMood;

public sealed record UpdateMoodCommand(int Id, string Name) : ICommand;
