using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.DeleteMusicGenre;

public sealed record DeleteMusicGenreCommand(int Id) : ICommand;
