using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.CreatePerformer;

public sealed record CreatePerformerCommand(
    string Name,
    string? AvatarUrl,
    string? Bio,
    string Type,
    IReadOnlyList<int> GenreIds
) : ICommand<int>;
