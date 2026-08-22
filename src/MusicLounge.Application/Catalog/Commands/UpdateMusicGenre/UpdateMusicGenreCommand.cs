using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.UpdateMusicGenre;

public sealed record UpdateMusicGenreCommand(int Id, string Name, string? NameEn) : ICommand;
