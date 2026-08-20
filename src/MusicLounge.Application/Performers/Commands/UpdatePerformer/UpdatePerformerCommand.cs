using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.UpdatePerformer;

public sealed record UpdatePerformerCommand(
    int PerformerId,
    string Name,
    string? AvatarUrl,
    string? Bio,
    string Type,
    IReadOnlyList<int> GenreIds
) : ICommand;
