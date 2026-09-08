using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.CreateMusicGenre;

public sealed record CreateMusicGenreCommand(string Name, string? NameEn) : ICommand<int>;
