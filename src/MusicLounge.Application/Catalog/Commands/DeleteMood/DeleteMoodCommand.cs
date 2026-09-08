using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.DeleteMood;

public sealed record DeleteMoodCommand(int Id) : ICommand;
