using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Performers.Commands.CreatePerformer;

public sealed record CreatePerformerCommand(
    string Name,
    string? AvatarUrl,
    string? Bio,
    string Type,
    IReadOnlyList<int> GenreIds,
    // MLACP-364: email lien lac cua nghe si — noi gui lien ket de nghe si tu xac nhan. Tuy chon.
    string? ContactEmail = null
) : ICommand<int>;
