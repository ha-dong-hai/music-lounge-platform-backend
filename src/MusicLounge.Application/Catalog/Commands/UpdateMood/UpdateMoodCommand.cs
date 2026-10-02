using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.UpdateMood;

public sealed record UpdateMoodCommand(Guid Id, string Name) : ICommand;
